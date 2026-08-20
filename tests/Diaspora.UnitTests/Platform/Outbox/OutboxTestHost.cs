using Diaspora.Contracts.Events;
using Diaspora.Persistence;
using Diaspora.Persistence.Outbox;
using Diaspora.Platform.Messaging.Outbox;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Dispatch;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Partitioning;
using Diaspora.Platform.Messaging.Outbox.Retry;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Messaging.Outbox.Tenancy;
using Diaspora.UnitTests.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The whole outbox over a real database, composed by hand.
/// <para>
/// <b>These tests would be worth very little against substitutes.</b> Claiming is a conditional
/// <c>UPDATE</c> whose correctness under two competing workers is a property of the SQL, not of
/// the C#; the partition-blocking predicate is a correlated subquery; the retention purge is a
/// two-statement bounded delete. A mock of <c>IOutboxStore</c> would happily agree with an
/// implementation that did none of it correctly.
/// </para>
/// <para>
/// SQLite rather than PostgreSQL for the same reason the integration suite chose it: no Docker
/// daemon, no container pull, so these run on every commit rather than when somebody remembers.
/// The claim deliberately uses no provider-specific construct — no <c>SKIP LOCKED</c> — which is
/// what makes that trade honest.
/// </para>
/// </summary>
public sealed class OutboxTestHost : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _dbOptions;
    private readonly OutboxOptions _options;

    public OutboxTestHost(Action<OutboxOptions>? configure = null)
    {
        _options = new OutboxOptions();

        // Jitter off so a test can assert on a scheduled time rather than a range.
        _options.Retry.BackoffJitter = 0;

        configure?.Invoke(_options);

        Clock = new FakeClock(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        Correlation = new FakeCorrelationContext();
        Transport = new RecordingOutboxTransport();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public FakeClock Clock { get; }

    public FakeCorrelationContext Correlation { get; }

    public RecordingOutboxTransport Transport { get; }

    public OutboxOptions Options => _options;

    /// <summary>
    /// The test aggregate, mapped through the platform's own contributor seam.
    /// <para>
    /// Borrowed from the B1.0.1 suite rather than invented, so the atomicity tests write a real
    /// aggregate — audited, soft-deletable, with a concurrency token and owned value objects —
    /// beside the outbox row. An atomicity test whose "aggregate" is a two-column table proves
    /// nothing about the transaction a real command opens.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<Diaspora.Persistence.Modules.IDatabaseContributor> Contributors =
        [new Diaspora.UnitTests.Persistence.SoftDeleteTestDatabaseContributor()];

    /// <summary>A fresh context with an empty change tracker over the same database.</summary>
    public DiasporaDbContext NewContext() =>
        new(_dbOptions, currentUser: null, Clock, eventDispatcher: null, Contributors);

    /// <summary>
    /// Writes events the way a producer does: into a context, saved once, as one transaction.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> EnqueueAsync(params IIntegrationEvent[] events)
    {
        using var context = NewContext();

        var writer = new EfOutboxWriter(context, Factory());

        var ids = events.Select(writer.Enqueue).ToArray();

        await context.SaveChangesAsync();

        return ids;
    }

    /// <summary>A dispatcher over its own context, as the hosted service would create it.</summary>
    public OutboxDispatcher Dispatcher(DiasporaDbContext? context = null)
    {
        var owned = context ?? NewContext();
        var wrapped = Options_();

        return new OutboxDispatcher(
            new EfOutboxStore(owned),
            new EfOutboxQueries(owned),
            Serializer(),
            Transport,
            new ExponentialBackoffRetryPolicy(wrapped),
            new SingleTenantOutboxTenantResolver(),
            new FakeOutboxWorkerIdentity(),
            new OutboxStatisticsSnapshot(),
            Clock,
            wrapped,
            NullLogger<OutboxDispatcher>.Instance);
    }

    /// <summary>
    /// A second dispatcher with a distinct worker identity and its own connection, for the
    /// competing-consumers tests.
    /// </summary>
    public OutboxDispatcher CompetingDispatcher(string workerId)
    {
        var owned = NewContext();
        var wrapped = Options_();

        return new OutboxDispatcher(
            new EfOutboxStore(owned),
            new EfOutboxQueries(owned),
            Serializer(),
            Transport,
            new ExponentialBackoffRetryPolicy(wrapped),
            new SingleTenantOutboxTenantResolver(),
            new FakeOutboxWorkerIdentity(workerId),
            new OutboxStatisticsSnapshot(),
            Clock,
            wrapped,
            NullLogger<OutboxDispatcher>.Instance);
    }

    public static EfOutboxStore Store(DiasporaDbContext context) => new(context);

    public static EfOutboxQueries Queries(DiasporaDbContext context) => new(context);

    public static EfOutboxMaintenance Maintenance(DiasporaDbContext context) => new(context);

    public OutboxMessageFactory Factory() => new(
        Serializer(),
        Resolver(),
        new OutboxPartitionKeyResolver([new ConventionOutboxPartitionKeyStrategy()]),
        new SingleTenantOutboxTenantResolver(),
        Correlation,
        Clock,
        Options_());

    /// <summary>Every message currently in the delivery table, in write order.</summary>
    public async Task<IReadOnlyList<OutboxMessage>> MessagesAsync()
    {
        using var context = NewContext();

        return await context.Set<OutboxMessage>()
            .AsNoTracking()
            .OrderBy(message => message.Sequence)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OutboxDeadLetter>> DeadLettersAsync()
    {
        using var context = NewContext();

        return await context.Set<OutboxDeadLetter>()
            .AsNoTracking()
            .OrderBy(deadLetter => deadLetter.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OutboxDeliveryAttempt>> AttemptsAsync()
    {
        using var context = NewContext();

        return await context.Set<OutboxDeliveryAttempt>()
            .AsNoTracking()
            .OrderBy(attempt => attempt.Id)
            .ToListAsync();
    }

    private static IntegrationEventTypeResolver? _resolver;

    private static IntegrationEventTypeResolver Resolver() =>
        _resolver ??= new IntegrationEventTypeResolver([typeof(WidgetChangedIntegrationEvent).Assembly]);

    private static JsonOutboxSerializer Serializer() => new(Resolver());

    private IOptions<OutboxOptions> Options_() => Microsoft.Extensions.Options.Options.Create(_options);

    public void Dispose() => _connection.Dispose();
}

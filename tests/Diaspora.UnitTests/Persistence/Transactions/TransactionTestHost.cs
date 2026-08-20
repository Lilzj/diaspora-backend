using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// A real <see cref="DiasporaDbContext"/> over SQLite, wired with a <em>retrying</em> execution
/// strategy and a fault injector.
/// <para>
/// The point is the strategy. Every guarantee B1.0.2 adds — commit verification, the receipt, the
/// replay guard — is dead code under a non-retrying strategy, which is what SQLite ships and
/// therefore what the rest of the suite exercises. Configuring one here is the supported EF Core
/// seam for reaching that code without a PostgreSQL container, and it makes the failure injection
/// deterministic instead of a race.
/// </para>
/// </summary>
public sealed class TransactionTestHost : IDisposable
{
    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new SoftDeleteTestDatabaseContributor()];

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _options;

    public TransactionTestHost(int maxRetryCount = 3, IDomainEventDispatcher? eventDispatcher = null)
    {
        Clock = new FakeClock(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        CurrentUser = FakeCurrentUser.Authenticated(UserId.New());
        Faults = new TransactionFaultInjector();
        EventDispatcher = eventDispatcher;

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(
                _connection,
                sqlite => sqlite.ExecutionStrategy(
                    dependencies => new FaultInjectingExecutionStrategy(dependencies, maxRetryCount)))
            .AddInterceptors(new CommitAcknowledgementFaultInterceptor(Faults))
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public FakeClock Clock { get; }

    public FakeCurrentUser CurrentUser { get; }

    public TransactionFaultInjector Faults { get; }

    public IDomainEventDispatcher? EventDispatcher { get; }

    /// <summary>A fresh context with an empty change tracker over the same database.</summary>
    public DiasporaDbContext NewContext() =>
        new(_options, CurrentUser, Clock, EventDispatcher, Contributors);

    /// <summary>How many orders are actually on disk. The duplicate-persistence assertion.</summary>
    public int CountOrders()
    {
        using var context = NewContext();

        return context.Set<SoftDeleteTestOrder>().IgnoreQueryFilters().Count();
    }

    /// <summary>How many transaction receipts were written. Exposes the verification path's cost.</summary>
    public int CountReceipts()
    {
        using var context = NewContext();

        return context.Set<Diaspora.Platform.Transactions.TransactionReceipt>().Count();
    }

    /// <summary>How many outbox messages are on disk. For the B1.1 compatibility tests.</summary>
    public int CountOutboxMessages()
    {
        using var context = NewContext();

        return context.Set<Diaspora.Platform.Messaging.Outbox.Model.OutboxMessage>().Count();
    }

    /// <summary>
    /// An outbox writer over the supplied context, composed exactly as the container composes it.
    /// <para>
    /// Built here rather than mocked because the B1.1 interaction under test is precisely that the
    /// outbox row joins the caller's transaction — which is a property of sharing the context, and
    /// a substitute would satisfy the test while removing the guarantee.
    /// </para>
    /// </summary>
    public Diaspora.Persistence.Outbox.EfOutboxWriter OutboxWriter(DiasporaDbContext context)
    {
        var resolver = new Diaspora.Platform.Messaging.Outbox.Serialization.IntegrationEventTypeResolver(
            [typeof(Diaspora.UnitTests.Platform.Outbox.WidgetChangedIntegrationEvent).Assembly]);

        var factory = new Diaspora.Platform.Messaging.Outbox.OutboxMessageFactory(
            new Diaspora.Platform.Messaging.Outbox.Serialization.JsonOutboxSerializer(resolver),
            resolver,
            new Diaspora.Platform.Messaging.Outbox.Partitioning.OutboxPartitionKeyResolver(
                [new Diaspora.Platform.Messaging.Outbox.Partitioning.ConventionOutboxPartitionKeyStrategy()]),
            new Diaspora.Platform.Messaging.Outbox.Tenancy.SingleTenantOutboxTenantResolver(),
            new FakeCorrelationContext(),
            Clock,
            Microsoft.Extensions.Options.Options.Create(
                new Diaspora.Platform.Messaging.Outbox.Options.OutboxOptions()));

        return new Diaspora.Persistence.Outbox.EfOutboxWriter(context, factory);
    }

    public void Dispose() => _connection.Dispose();
}

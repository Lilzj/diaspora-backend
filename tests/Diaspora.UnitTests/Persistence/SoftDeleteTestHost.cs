using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// A real <see cref="DiasporaDbContext"/> over an isolated in-memory SQLite database.
/// <para>
/// The connection is held open for the host's lifetime because an in-memory SQLite database is
/// destroyed the moment its last connection closes — the same reason the integration suite's
/// factory holds one.
/// </para>
/// <para>
/// Each call to <see cref="NewContext"/> returns a <em>fresh</em> context over the <em>same</em>
/// database. That is what makes the multi-cycle tests meaningful: reading back through a new
/// context with an empty change tracker is the only way to observe what was actually written
/// rather than what happens to still be in memory.
/// </para>
/// </summary>
public sealed class SoftDeleteTestHost : IDisposable
{
    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new SoftDeleteTestDatabaseContributor()];

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DiasporaDbContext> _options;

    public SoftDeleteTestHost(DateTimeOffset? now = null, UserId? currentUser = null)
    {
        Clock = new FakeClock(now ?? new DateTimeOffset(2026, 7, 28, 9, 0, 0, TimeSpan.Zero));
        CurrentUser = FakeCurrentUser.Authenticated(currentUser ?? UserId.New());

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public FakeClock Clock { get; }

    public FakeCurrentUser CurrentUser { get; }

    /// <summary>A fresh context with an empty change tracker over the same database.</summary>
    public DiasporaDbContext NewContext() =>
        new(_options, CurrentUser, Clock, eventDispatcher: null, Contributors);

    /// <summary>Seeds one order and returns its identifier.</summary>
    public SoftDeleteTestOrderId Seed(SoftDeleteTestOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        using var context = NewContext();

        context.Add(order);
        context.SaveChanges();

        return order.Id;
    }

    /// <summary>
    /// Loads an order including its owned collection, ignoring the soft-delete query filter so a
    /// test can inspect what a deletion actually wrote rather than being told the row is gone.
    /// </summary>
    public static SoftDeleteTestOrder? Load(
        DiasporaDbContext context, SoftDeleteTestOrderId id, bool includeDeleted = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var query = context.Set<SoftDeleteTestOrder>().Include(order => order.Lines);

        return includeDeleted
            ? query.IgnoreQueryFilters().FirstOrDefault(order => order.Id == id)
            : query.FirstOrDefault(order => order.Id == id);
    }

    public void Dispose() => _connection.Dispose();
}

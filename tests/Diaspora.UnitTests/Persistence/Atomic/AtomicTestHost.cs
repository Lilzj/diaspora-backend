using Diaspora.Persistence;
using Diaspora.Persistence.Atomic;
using Diaspora.Persistence.Atomic.Reservations;
using Diaspora.Persistence.Modules;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// A real <see cref="DiasporaDbContext"/> over a real SQLite database, on disk.
/// <para>
/// <b>On disk rather than in memory, and that is the whole reason this host exists separately.</b>
/// An in-memory SQLite database lives inside one connection, so every context sharing it shares a
/// connection — and two "concurrent" transactions over one connection are not concurrent at all.
/// Testing a primitive whose entire purpose is to arbitrate between two transactions against a
/// database that cannot have two would prove nothing. A file gives each context its own connection
/// and its own transaction, which is the only arrangement in which the guarded update is doing any
/// work.
/// </para>
/// </summary>
public sealed class AtomicTestHost : IDisposable
{
    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new AtomicTestDatabaseContributor()];

    private readonly string _path;
    private readonly DbContextOptions<DiasporaDbContext> _options;

    public AtomicTestHost(DateTimeOffset? now = null)
    {
        Clock = new FakeClock(now ?? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero));
        CurrentUser = FakeCurrentUser.Authenticated(UserId.New());

        _path = Path.Combine(
            Path.GetTempPath(), $"diaspora-atomic-{Guid.NewGuid():N}.db");

        // The busy timeout is what makes a contention test measure contention rather than SQLite's
        // refusal to queue: without it a second writer fails immediately with SQLITE_BUSY, which
        // looks exactly like a refused guard and would make the suite green for the wrong reason.
        //
        // Pooling stays on, because the throughput figures below go in the performance report and
        // a connection opened per caller would measure the file system rather than the primitive.
        // The cost is that Dispose may not be able to delete the file until the process exits,
        // which is why it fails quietly. Clearing the pool instead is not an option:
        // SqliteConnection.ClearAllPools is process-wide, and in a suite that runs collections in
        // parallel it tears down every other host's in-memory database along with this one's file.
        _options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite($"Data Source={_path};Default Timeout=30")
            .ReplaceService<IModelCacheKeyFactory, AtomicTestModelCacheKeyFactory>()
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();

        // Write-ahead logging lets readers run while a writer holds the file, which is how the
        // production providers behave and the closest SQLite gets to it.
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    public FakeClock Clock { get; }

    public FakeCurrentUser CurrentUser { get; }

    /// <summary>A fresh context, with its own connection, over the same database.</summary>
    public DiasporaDbContext NewContext() =>
        new(_options, CurrentUser, Clock, eventDispatcher: null, Contributors);

    public static IAtomicUpdateExecutor Executor(DiasporaDbContext context) =>
        new EfAtomicUpdateExecutor(context);

    public static IAtomicReservationStore Store(DiasporaDbContext context) =>
        new AtomicReservationStore(Executor(context));

    /// <summary>The capacity every test in this folder guards.</summary>
    public static AtomicCapacity<AtomicTestResource> Capacity { get; } =
        AtomicCapacity<AtomicTestResource>.Of(
            resource => resource.Capacity.Total,
            resource => resource.Capacity.Held,
            resource => resource.Capacity.Consumed);

    public AtomicTestResourceId Seed(int total, string name = "resource")
    {
        using var context = NewContext();

        var resource = AtomicTestResource.Create(name, total);

        context.Add(resource);
        context.SaveChanges();

        return resource.Id;
    }

    public AtomicTestResource Load(AtomicTestResourceId id)
    {
        using var context = NewContext();

        return context.Set<AtomicTestResource>().AsNoTracking().Single(r => r.Id == id);
    }

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A temporary file the operating system will collect. Failing a passing test
                // because Windows had not released a handle yet would be noise.
            }
        }
    }
}

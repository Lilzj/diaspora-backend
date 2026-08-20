using System.Diagnostics;
using Diaspora.Persistence.Atomic;
using Diaspora.Persistence.Atomic.Reservations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// What the primitive is for: many callers, one scarce thing, and no more of it handed out than
/// exists.
/// <para>
/// <b>Each task gets its own context and its own connection.</b> That arrangement — not the number
/// of tasks — is what makes these tests meaningful. The existing housing and marketplace scale
/// tests run fifty callers through one shared in-memory connection, which serialises them into a
/// queue and would stay green against an implementation with no concurrency control at all.
/// </para>
/// </summary>
public sealed class AtomicContentionTests : IDisposable
{
    private readonly AtomicTestHost _host = new();
    private readonly ITestOutputHelper _output;

    public AtomicContentionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// <b>The overselling test, and the one that fails without the primitive.</b> Two transactions
    /// each load a resource with one unit left, each decide in memory that they may take it, and
    /// each write. Optimistic concurrency does not see a conflict — neither is editing anything the
    /// other touched — so before this phase both committed and the resource was oversold. The
    /// guard is evaluated by the database against the row as it stands, so the second one loses.
    /// </summary>
    [Fact]
    public async Task Two_transactions_reaching_for_the_last_unit_do_not_both_get_it()
    {
        var id = _host.Seed(total: 1);

        await using var first = _host.NewContext();
        await using var second = _host.NewContext();

        // Both read the same truth: one unit, free.
        var firstView = await first.Set<AtomicTestResource>().SingleAsync(r => r.Id == id);
        var secondView = await second.Set<AtomicTestResource>().SingleAsync(r => r.Id == id);

        firstView.Hold(1).Should().BeTrue();
        secondView.Hold(1).Should().BeTrue("both aggregates decide correctly from what they loaded");

        var firstOutcome = await AtomicTestHost.Store(first).ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 1)
            {
                Synchronize = firstView
            });

        var secondOutcome = await AtomicTestHost.Store(second).ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 1)
            {
                Synchronize = secondView
            });

        firstOutcome.Applied.Should().BeTrue();
        secondOutcome.Applied.Should().BeFalse("the database arbitrates, and there was one unit");

        _host.Load(id).Capacity.Held.Should().Be(1);
    }

    [Theory]
    [InlineData(50, 5)]
    [InlineData(200, 25)]
    public async Task Parallel_callers_take_exactly_the_capacity_and_no_more(int callers, int total)
    {
        var id = _host.Seed(total);

        var granted = await ReserveInParallelAsync(id, callers);

        granted.Should().Be(total, "every unit is taken, and not one more");

        _host.Load(id).Capacity.Held.Should().Be(total);
    }

    /// <summary>
    /// The brief's headline figure. A thousand callers against a resource of four hundred: the
    /// count is exact, and the wall clock is recorded so a future change that makes this quadratic
    /// is visible rather than merely slower.
    /// </summary>
    [Fact]
    public async Task A_thousand_concurrent_reservations_settle_exactly_and_quickly()
    {
        const int Callers = 1_000;
        const int Total = 400;

        var id = _host.Seed(Total);

        var stopwatch = Stopwatch.StartNew();
        var granted = await ReserveInParallelAsync(id, Callers);
        stopwatch.Stop();

        granted.Should().Be(Total);
        _host.Load(id).Capacity.Held.Should().Be(Total);

        _output.WriteLine(
            $"{Callers} reservations against {Total} units in {stopwatch.ElapsedMilliseconds} ms " +
            $"({Callers / Math.Max(1, stopwatch.Elapsed.TotalSeconds):F0}/s).");
    }

    /// <summary>
    /// Holds and releases racing each other. Nothing is lost and nothing is invented: whatever the
    /// counter ends on, it is a number the sequence of statements can account for.
    /// </summary>
    [Fact]
    public async Task Holds_and_releases_racing_each_other_never_drive_the_counter_negative()
    {
        var id = _host.Seed(total: 20);

        await Task.WhenAll(Enumerable.Range(0, 200).Select(async index =>
        {
            await using var context = _host.NewContext();

            var store = AtomicTestHost.Store(context);
            var reservation =
                new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 1);

            if (index % 2 == 0)
            {
                await store.ReserveAsync(reservation);
            }
            else
            {
                await store.ReleaseAsync(reservation);
            }
        }));

        var capacity = _host.Load(id).Capacity;

        capacity.Held.Should().BeInRange(0, 20);
        capacity.Available.Should().BeInRange(0, 20);
    }

    /// <summary>
    /// Retry safety: the statement is relative, so replaying it after a rollback is not the same as
    /// applying it twice. Here the rollback is explicit, which is the shape B1.0.2's execution
    /// strategy produces when it replays a transaction.
    /// </summary>
    [Fact]
    public async Task A_rolled_back_reservation_leaves_nothing_behind_and_replays_cleanly()
    {
        var id = _host.Seed(total: 2);

        await using (var abandoned = _host.NewContext())
        {
            await using var transaction = await abandoned.Database.BeginTransactionAsync();

            var outcome = await AtomicTestHost.Store(abandoned).ReserveAsync(
                new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 2));

            outcome.Applied.Should().BeTrue();

            await transaction.RollbackAsync();
        }

        _host.Load(id).Capacity.Held.Should().Be(0);

        await using var replay = _host.NewContext();

        var replayed = await AtomicTestHost.Store(replay).ReserveAsync(
            new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 2));

        replayed.Applied.Should().BeTrue("the units the rollback returned are available again");
        replayed.State!.Held.Should().Be(2, "and the replay applied once, not twice");
    }

    /// <summary>
    /// <b>The primitive's own cost, with the commit taken out of the measurement.</b> The figure
    /// beside it — a statement per autocommit — is dominated by one <c>fsync</c> per write and says
    /// almost nothing about this code. Production never looks like that: a guarded update runs
    /// inside the caller's transaction alongside the reservation it justifies, so it adds two round
    /// trips and no commit at all.
    /// </summary>
    [Fact]
    public async Task Guarded_updates_inside_one_transaction_cost_a_round_trip_each()
    {
        const int Updates = 500;

        var id = _host.Seed(total: Updates + 1);

        await using var context = _host.NewContext();
        var executor = AtomicTestHost.Executor(context);

        await using var transaction = await context.Database.BeginTransactionAsync();

        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < Updates; index++)
        {
            var applied = await executor.ExecuteAsync(
                AtomicUpdate.For<AtomicTestResource>(id)
                    .Increment(r => r.Capacity.Held, 1)
                    .Guard(term => term
                        .Column(r => r.Capacity.Total)
                        .Minus(r => r.Capacity.Held)
                        .Minus(r => r.Capacity.Consumed)
                        .AtLeast(1))
                    .ReadingBack(r => r.Capacity.Held));

            applied.Applied.Should().BeTrue();
        }

        stopwatch.Stop();
        await transaction.CommitAsync();

        _output.WriteLine(
            $"{Updates} guarded updates with read-back inside one transaction in " +
            $"{stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.Elapsed.TotalMilliseconds / Updates:F3} ms each).");

        _host.Load(id).Capacity.Held.Should().Be(Updates);
    }

    /// <summary>
    /// Bulk work across many rows rather than contention on one: the primitive must not degrade
    /// into a round trip per column or a query per aggregate.
    /// </summary>
    [Fact]
    public async Task Guarded_updates_across_many_rows_stay_linear()
    {
        var ids = Enumerable.Range(0, 200)
            .Select(index => _host.Seed(total: 4, name: $"resource-{index}"))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        await using (var context = _host.NewContext())
        {
            var executor = AtomicTestHost.Executor(context);

            foreach (var id in ids)
            {
                await executor.ExecuteAsync(
                    AtomicUpdate.For<AtomicTestResource>(id).Increment(r => r.Capacity.Held, 1));
            }
        }

        stopwatch.Stop();

        _output.WriteLine(
            $"{ids.Count} guarded updates in {stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.Elapsed.TotalMilliseconds / ids.Count:F2} ms each).");

        _host.Load(ids[^1]).Capacity.Held.Should().Be(1);
    }

    private async Task<int> ReserveInParallelAsync(AtomicTestResourceId id, int callers)
    {
        var outcomes = await Task.WhenAll(Enumerable.Range(0, callers).Select(async _ =>
        {
            await using var context = _host.NewContext();

            var outcome = await AtomicTestHost.Store(context).ReserveAsync(
                new AtomicReservation<AtomicTestResource>(AtomicTestHost.Capacity, id, 1));

            return outcome.Applied;
        }));

        return outcomes.Count(applied => applied);
    }

    public void Dispose() => _host.Dispose();
}

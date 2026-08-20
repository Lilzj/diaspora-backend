using Diaspora.Persistence.Atomic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// The primitive's own behaviour, against a real database and a real model.
/// <para>
/// Every assertion here is about something that could only be checked by executing SQL: that the
/// column behind an owned property was found, that a strongly typed identifier reached the
/// <c>WHERE</c> clause in its stored form, that the guard was evaluated by the database rather than
/// by C#. A test double of the executor would restate its own implementation.
/// </para>
/// </summary>
public sealed class AtomicUpdateExecutorTests : IDisposable
{
    private readonly AtomicTestHost _host = new();

    // ── The guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_increment_within_capacity_applies()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Increment(r => r.Capacity.Held, 3)
                .Guard(term => term
                    .Column(r => r.Capacity.Total)
                    .Minus(r => r.Capacity.Held)
                    .Minus(r => r.Capacity.Consumed)
                    .AtLeast(3)));

        result.Applied.Should().BeTrue();
        result.RowsAffected.Should().Be(1);

        _host.Load(id).Capacity.Held.Should().Be(3);
    }

    /// <summary>
    /// <b>The assertion the whole phase exists for.</b> The row says five; the request asks for six;
    /// the database refuses without anybody in C# having looked.
    /// </summary>
    [Fact]
    public async Task An_increment_beyond_capacity_is_refused_by_the_database()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Increment(r => r.Capacity.Held, 6)
                .Guard(term => term
                    .Column(r => r.Capacity.Total)
                    .Minus(r => r.Capacity.Held)
                    .Minus(r => r.Capacity.Consumed)
                    .AtLeast(6)));

        result.Applied.Should().BeFalse();
        result.RowsAffected.Should().Be(0);

        _host.Load(id).Capacity.Held.Should().Be(0, "a refused guard changes nothing");
    }

    [Fact]
    public async Task A_missing_row_is_reported_the_same_way_as_a_refused_guard()
    {
        await using var context = _host.NewContext();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(AtomicTestResourceId.New())
                .Increment(r => r.Capacity.Held, 1));

        result.Applied.Should().BeFalse();
    }

    // ── Clamped forms ────────────────────────────────────────────────────

    [Fact]
    public async Task A_floored_decrement_never_goes_below_zero()
    {
        var id = _host.Seed(total: 5);

        await using var context = _host.NewContext();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .DecrementToZero(r => r.Capacity.Held, 3));

        result.Applied.Should().BeTrue("a release must never be refused");

        _host.Load(id).Capacity.Held.Should().Be(0);
    }

    /// <summary>
    /// Both halves of a move read the row as it was before the statement, so a drifted pair of
    /// counters transfers what is actually there rather than what the caller asked for.
    /// </summary>
    [Fact]
    public async Task A_limited_move_transfers_only_what_the_source_holds()
    {
        var id = _host.Seed(total: 10);

        await using (var seed = _host.NewContext())
        {
            await AtomicTestHost.Executor(seed).ExecuteAsync(
                AtomicUpdate.For<AtomicTestResource>(id).Increment(r => r.Capacity.Held, 2));
        }

        await using var context = _host.NewContext();

        await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .DecrementToZero(r => r.Capacity.Held, 5)
                .IncrementLimited(r => r.Capacity.Consumed, 5, r => r.Capacity.Held));

        var resource = _host.Load(id);

        resource.Capacity.Held.Should().Be(0);
        resource.Capacity.Consumed.Should().Be(2, "only two units were there to move");
    }

    // ── Compare-and-update, and conditional state transitions ────────────

    [Fact]
    public async Task A_compare_and_update_applies_only_against_the_expected_value()
    {
        var id = _host.Seed(total: 1);

        await using var context = _host.NewContext();
        var executor = AtomicTestHost.Executor(context);

        var stale = await executor.ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Set(r => r.Name, "renamed")
                .Requiring(r => r.Revision, 7));

        stale.Applied.Should().BeFalse();

        var current = await executor.ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Set(r => r.Name, "renamed")
                .Requiring(r => r.Revision, 0));

        current.Applied.Should().BeTrue();

        _host.Load(id).Name.Should().Be("renamed");
    }

    /// <summary>
    /// The transition guard reads a converted column: the enum is stored as text, and the parameter
    /// has to arrive in that form or the comparison silently never matches.
    /// </summary>
    [Fact]
    public async Task A_conditional_state_transition_reads_the_stored_form_of_the_column()
    {
        var id = _host.Seed(total: 1);

        await using var context = _host.NewContext();
        var executor = AtomicTestHost.Executor(context);

        var wrongState = await executor.ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Set(r => r.Status, AtomicTestStatus.Closed)
                .Requiring(r => r.Status, AtomicTestStatus.Committed));

        wrongState.Applied.Should().BeFalse();

        var rightState = await executor.ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Set(r => r.Status, AtomicTestStatus.Committed)
                .Requiring(r => r.Status, AtomicTestStatus.Open));

        rightState.Applied.Should().BeTrue();

        _host.Load(id).Status.Should().Be(AtomicTestStatus.Committed);
    }

    // ── Read-back and reconciliation ─────────────────────────────────────

    [Fact]
    public async Task The_read_back_reports_the_counts_the_database_now_holds()
    {
        var id = _host.Seed(total: 4);

        await using var context = _host.NewContext();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Increment(r => r.Capacity.Held, 2)
                .ReadingBack(r => r.Capacity.Held)
                .ReadingBack(r => r.Capacity.Total));

        result.Value("Capacity.Held").Should().Be(2);
        result.Value("Capacity.Total").Should().Be(4);
    }

    /// <summary>
    /// <b>The defect this reconciliation exists to prevent, demonstrated in one test.</b> The
    /// aggregate holds two units in memory; the database has already been told about four by
    /// somebody else. Without reconciliation the caller's own <c>SaveChanges</c> writes its two over
    /// the database's six, and the guarantee established microseconds earlier is gone.
    /// </summary>
    [Fact]
    public async Task Saving_after_a_reconciled_update_does_not_write_the_stale_count_back()
    {
        var id = _host.Seed(total: 10);

        await using var context = _host.NewContext();

        var resource = await context.Set<AtomicTestResource>().SingleAsync(r => r.Id == id);

        // Somebody else takes four units, on their own connection, and commits.
        await using (var other = _host.NewContext())
        {
            await AtomicTestHost.Executor(other).ExecuteAsync(
                AtomicUpdate.For<AtomicTestResource>(id).Increment(r => r.Capacity.Held, 4));
        }

        // Our aggregate decides in memory, from the count it loaded, and is then reconciled.
        resource.Hold(2).Should().BeTrue();

        var result = await AtomicTestHost.Executor(context).ExecuteAsync(
            AtomicUpdate.For<AtomicTestResource>(id)
                .Increment(r => r.Capacity.Held, 2)
                .ReadingBack(r => r.Capacity.Held)
                .Synchronizing(resource));

        result.Value("Capacity.Held").Should().Be(6);

        resource.Capacity.Held.Should().Be(
            6, "the tracked instance is reconciled with what the database holds");

        // The mutation the caller made for its own reasons still saves.
        resource.MoveTo(AtomicTestStatus.Committed);

        await context.SaveChangesAsync();

        var persisted = _host.Load(id);

        persisted.Capacity.Held.Should().Be(6, "the reconciled column must not be written back");
        persisted.Status.Should().Be(AtomicTestStatus.Committed);
    }

    /// <summary>
    /// A guarded update is not an edit, and must not fail because somebody else is editing. The
    /// concurrency token is deliberately outside the statement's WHERE clause.
    /// </summary>
    [Fact]
    public async Task A_guarded_update_does_not_trip_the_concurrency_token()
    {
        var id = _host.Seed(total: 3);

        await using var editor = _host.NewContext();

        var loaded = await editor.Set<AtomicTestResource>().SingleAsync(r => r.Id == id);

        await using (var buyer = _host.NewContext())
        {
            await AtomicTestHost.Executor(buyer).ExecuteAsync(
                AtomicUpdate.For<AtomicTestResource>(id).Increment(r => r.Capacity.Held, 1));
        }

        loaded.Rename("still editable");

        var save = async () => await editor.SaveChangesAsync();

        await save.Should().NotThrowAsync(
            "a hold taken by a buyer must not fail the seller's in-flight edit");
    }

    public void Dispose() => _host.Dispose();
}

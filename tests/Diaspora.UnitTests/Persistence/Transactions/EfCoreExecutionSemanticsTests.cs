using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// The EF Core behaviours B1.0.2's design rests on, pinned as tests.
/// <para>
/// <b>These are not tests of our code.</b> They are executable statements of what EF Core does,
/// written because the B1.0.2 design turns on two facts that are easy to assume wrongly and
/// expensive to be wrong about. If a future EF Core release changes either, the design's
/// justification changes with it — and a red test here is a much better way to find that out than
/// a duplicated row in production.
/// </para>
/// </summary>
public sealed class EfCoreExecutionSemanticsTests
{
    /// <summary>
    /// <b>Fact 1, and the reason the documented "accept at the end" pattern does not fit this
    /// platform.</b>
    /// <para>
    /// EF Core's connection-resiliency guidance for manual transactions is to call
    /// <c>SaveChanges(acceptAllChangesOnSuccess: false)</c> inside the transaction and
    /// <c>ChangeTracker.AcceptAllChanges()</c> after the commit, so that a replay re-runs against a
    /// change tracker that still holds the pending work.
    /// </para>
    /// <para>
    /// That pattern assumes <em>one</em> save per transaction. This platform's save path performs
    /// several — the aggregate, then whatever the domain event handlers wrote, then whatever their
    /// events cascaded into — because integration events do not exist until after the first save.
    /// With acceptance deferred, the second save re-sends the first save's commands, and the
    /// aggregate is inserted twice.
    /// </para>
    /// </summary>
    [Fact]
    public void Deferring_acceptance_makes_a_second_save_re_send_the_first_saves_writes()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-1"));

        // The documented pattern's inner save.
        context.SaveChanges(acceptAllChangesOnSuccess: false);

        // The entity is still Added, because nothing accepted the change.
        context.ChangeTracker.Entries<SoftDeleteTestOrder>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Added);

        // A second save in the same transaction — which is what this platform's dispatch loop
        // does — therefore tries to insert it again.
        var act = () => context.SaveChanges(acceptAllChangesOnSuccess: false);

        act.Should().Throw<DbUpdateException>(
            "the row is already there; EF re-sends the INSERT because the entry is still Added");
    }

    /// <summary>
    /// The counterpart: with acceptance on, the second save is a no-op for the first save's
    /// entities. This is what the platform does today, and it is why the change tracker is
    /// <em>hollow</em> by the time a later failure could trigger a replay.
    /// </summary>
    [Fact]
    public void Accepting_makes_a_second_save_a_no_op_and_leaves_nothing_to_replay()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-2"));

        context.SaveChanges(acceptAllChangesOnSuccess: true);

        context.ChangeTracker.Entries<SoftDeleteTestOrder>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged);

        // Nothing left to write — and nothing left to replay either, which is precisely the
        // hazard B1.0.2 exists to make impossible rather than merely unlikely.
        context.SaveChanges().Should().Be(0);
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    /// <summary>
    /// <b>Fact 2: whether a provider retries is a capability, not a provider name.</b>
    /// <para>
    /// <c>IExecutionStrategy.RetriesOnFailure</c> is the supported way to ask. Keying the
    /// platform's behaviour off it rather than off a provider string is what keeps the reliability
    /// patch provider-agnostic — SQLite, PostgreSQL without <c>EnableRetryOnFailure</c>, and a
    /// provider that does not exist yet all answer correctly without the platform naming any of
    /// them.
    /// </para>
    /// </summary>
    [Fact]
    public void Sqlites_default_execution_strategy_does_not_retry()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeFalse();
    }

    /// <summary>
    /// And a retrying strategy says so, whatever provider it is attached to. The test harness
    /// exercises the verified path on SQLite by configuring one, which is the only way this suite
    /// can cover the code that only PostgreSQL would otherwise reach.
    /// </summary>
    [Fact]
    public void A_configured_retrying_strategy_reports_that_it_retries()
    {
        using var host = new TransactionTestHost();
        using var context = host.NewContext();

        var strategy = context.Database.CreateExecutionStrategy();

        strategy.Should().BeAssignableTo<IExecutionStrategy>();
        strategy.RetriesOnFailure.Should().BeTrue();
    }
}

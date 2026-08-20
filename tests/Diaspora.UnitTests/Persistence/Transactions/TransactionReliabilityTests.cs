using Diaspora.Domain.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// B1.0.2's central claim, under a retrying execution strategy with faults injected at each of the
/// three windows a transaction can fail in.
/// <para>
/// <b>Every test here fails against <c>verifySucceeded: null</c>,</b> which is what the platform
/// passed until this phase. The most important one —
/// <see cref="A_lost_commit_acknowledgement_is_verified_rather_than_replayed"/> — fails by writing
/// the aggregate twice, silently, with the request reporting success.
/// </para>
/// <para>
/// Attempts are counted by the operation delegate itself rather than by the strategy. The strategy
/// runs every EF operation in the process — schema creation, each query — so a counter there
/// answers a different question from the one these tests ask.
/// </para>
/// </summary>
public sealed class TransactionReliabilityTests
{
    // ── The window that motivated the phase ──────────────────────────────

    /// <summary>
    /// The commit reaches the server and succeeds; the acknowledgement is lost on the way back.
    /// <para>
    /// A retrying strategy with nothing to verify against sees a transient failure and replays,
    /// which inserts everything a second time. With a receipt written inside the transaction, the
    /// platform asks the database whether the commit landed, learns that it did, and returns the
    /// result it had already captured. One row, one success, no replay.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_lost_commit_acknowledgement_is_verified_rather_than_replayed()
    {
        using var host = new TransactionTestHost();

        host.Faults.FailCommitAcknowledgement();

        using var context = host.NewContext();

        var attempts = 0;

        var written = await context.ExecuteTransactionalAsync(async token =>
        {
            attempts++;
            context.Add(SoftDeleteTestOrder.Create("ORD-1"));
            return await context.SaveChangesAsync(token);
        });

        written.Should().Be(1, "the caller gets the result of the work that really did commit");

        host.Faults.AcknowledgementFailures.Should().Be(1, "the failure really was injected");
        attempts.Should().Be(1, "verification must replace the replay, not follow it");

        host.CountOrders().Should().Be(1, "the aggregate must be written exactly once");
    }

    /// <summary>
    /// The same scenario through the path the platform actually takes: <c>SaveChangesAsync</c> with
    /// a dispatcher present, which opens the transaction itself and saves more than once inside it.
    /// A replay here would duplicate everything the loop wrote.
    /// </summary>
    [Fact]
    public async Task A_verified_commit_leaves_one_copy_of_a_dispatching_save()
    {
        using var host = new TransactionTestHost(eventDispatcher: new RecordingDomainEventDispatcher());

        host.Faults.FailCommitAcknowledgement();

        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-2"));

        await context.SaveChangesAsync();

        host.Faults.AcknowledgementFailures.Should().Be(1);
        host.CountOrders().Should().Be(1);
    }

    // ── The window a replay handles correctly ────────────────────────────

    /// <summary>
    /// A transient failure before anything is written leaves the change tracker holding every
    /// pending change, so the replay is the original operation and nobody notices.
    /// </summary>
    [Fact]
    public async Task A_failure_before_any_write_is_retried_transparently()
    {
        using var host = new TransactionTestHost();

        host.Faults.FailBeforeSave(times: 2);

        using var context = host.NewContext();

        var order = SoftDeleteTestOrder.Create("ORD-3");
        var attempts = 0;

        var written = await context.ExecuteTransactionalAsync(async token =>
        {
            attempts++;

            host.Faults.ThrowIfFailingBeforeSave();

            if (context.Entry(order).State == EntityState.Detached)
            {
                context.Add(order);
            }

            return await context.SaveChangesAsync(token);
        });

        written.Should().Be(1);
        attempts.Should().Be(3, "two injected failures, then success");
        host.CountOrders().Should().Be(1, "the retries must not each leave a row behind");
    }

    // ── The window that used to lose data silently ───────────────────────

    /// <summary>
    /// <b>The hazard the guard exists for.</b> A transient failure lands after a save has been
    /// accepted and before the commit. The transaction rolls back, so the database is unchanged —
    /// but the change tracker is now empty, and a replay would run the operation against nothing,
    /// commit an empty transaction, and report success for work that was discarded.
    /// <para>
    /// The platform refuses, loudly. The caller gets a failure it can act on and the database is
    /// exactly as it was.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_failure_after_an_accepted_write_is_refused_rather_than_replayed()
    {
        using var host = new TransactionTestHost();

        host.Faults.FailAfterSave();

        using var context = host.NewContext();

        var attempts = 0;

        var act = async () => await context.ExecuteTransactionalAsync(async token =>
        {
            attempts++;

            context.Add(SoftDeleteTestOrder.Create("ORD-4"));

            var written = await context.SaveChangesAsync(token);

            host.Faults.ThrowIfFailingAfterSave();

            return written;
        });

        var failure = await act.Should().ThrowAsync<TransientTransactionFailureException>();

        failure.Which.InnerException.Should().BeOfType<TransientTestException>(
            "the refusal must name the real cause, not only its own verdict");

        attempts.Should().Be(1, "the guard fires before the second attempt runs any of the operation");

        host.CountOrders().Should().Be(0, "the transaction rolled back and nothing was replayed");
    }

    /// <summary>
    /// And the property that makes the refusal acceptable: the database is untouched. A replay
    /// that wrote nothing would have left the caller believing otherwise.
    /// </summary>
    [Fact]
    public async Task A_refused_replay_leaves_the_database_exactly_as_it_was()
    {
        using var host = new TransactionTestHost();

        using (var seed = host.NewContext())
        {
            seed.Add(SoftDeleteTestOrder.Create("EXISTING"));
            await seed.SaveChangesAsync();
        }

        host.CountOrders().Should().Be(1);

        host.Faults.FailAfterSave();

        using var context = host.NewContext();

        var act = async () => await context.ExecuteTransactionalAsync(async token =>
        {
            context.Add(SoftDeleteTestOrder.Create("ORD-5"));

            var written = await context.SaveChangesAsync(token);

            host.Faults.ThrowIfFailingAfterSave();

            return written;
        });

        await act.Should().ThrowAsync<TransientTransactionFailureException>();

        host.CountOrders().Should().Be(1, "only the row that was already there");
    }

    // ── Ordinary behaviour is unchanged ──────────────────────────────────

    [Fact]
    public async Task A_transaction_that_does_not_fail_commits_once_and_writes_one_receipt()
    {
        using var host = new TransactionTestHost();
        using var context = host.NewContext();

        var attempts = 0;

        await context.ExecuteTransactionalAsync(async token =>
        {
            attempts++;
            context.Add(SoftDeleteTestOrder.Create("ORD-6"));
            return await context.SaveChangesAsync(token);
        });

        attempts.Should().Be(1);
        host.CountOrders().Should().Be(1);
        host.CountReceipts().Should().Be(1, "one receipt per transactional execution");
    }

    /// <summary>
    /// A non-transient failure is not the execution strategy's business. It surfaces unchanged and
    /// the transaction rolls back — the behaviour every handler already relies on.
    /// </summary>
    [Fact]
    public async Task A_business_failure_rolls_back_and_is_not_retried()
    {
        using var host = new TransactionTestHost();
        using var context = host.NewContext();

        var attempts = 0;

        var act = async () => await context.ExecuteTransactionalAsync<int>(async token =>
        {
            attempts++;

            context.Add(SoftDeleteTestOrder.Create("ORD-7"));

            await context.SaveChangesAsync(token);

            throw new InvalidOperationException("the rest of the command failed");
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("the rest of the command failed");

        attempts.Should().Be(1, "a non-transient failure is not retried");
        host.CountOrders().Should().Be(0);
    }

    /// <summary>
    /// A nested call joins the running transaction rather than opening its own — unchanged from
    /// before B1.0.2, and the reason a <c>[Transactional]</c> command and its handler's own save do
    /// not nest.
    /// </summary>
    [Fact]
    public async Task A_nested_execution_joins_the_transaction_already_open()
    {
        using var host = new TransactionTestHost();
        using var context = host.NewContext();

        await context.ExecuteTransactionalAsync(async token =>
        {
            context.HasActiveTransaction.Should().BeTrue();

            return await context.ExecuteTransactionalAsync(async inner =>
            {
                context.Add(SoftDeleteTestOrder.Create("ORD-8"));
                return await context.SaveChangesAsync(inner);
            }, token);
        });

        host.CountOrders().Should().Be(1);
        host.CountReceipts().Should().Be(1, "one receipt for the outer transaction only");
    }

    /// <summary>
    /// <b>Why the platform's coverage is complete, stated as a test.</b>
    /// <para>
    /// EF Core's own <c>SaveChanges</c> opens a transaction when a save spans more than one command
    /// batch, commits it, and retries the whole save under the execution strategy with no
    /// verification of its own. That is the same lost-acknowledgement hazard B1.0.2 fixes, one
    /// layer down, and the platform cannot patch it.
    /// </para>
    /// <para>
    /// It does not have to, because that path does not occur in a configured host: an event
    /// dispatcher is always registered, so every top-level save routes through
    /// <c>ExecuteTransactionalAsync</c> and the inner saves run inside a transaction the platform
    /// already owns — where EF opens none of its own and retries none of them. This test pins that
    /// routing. If a future change lets a top-level save skip the platform transaction, the gap
    /// reopens silently, and this is what would notice.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Every_top_level_save_opens_a_platform_transaction()
    {
        using var host = new TransactionTestHost(
            eventDispatcher: new RecordingDomainEventDispatcher());

        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-9"));

        await context.SaveChangesAsync();

        // A receipt exists only if the save went through the platform's verified transaction path.
        host.CountReceipts().Should().Be(
            1, "a top-level save must not be left to EF's own unverified transaction handling");
    }

    /// <summary>
    /// The retry budget is finite. A failure that keeps recurring before any write surfaces to the
    /// caller rather than looping.
    /// </summary>
    [Fact]
    public async Task A_failure_that_never_clears_surfaces_rather_than_looping()
    {
        using var host = new TransactionTestHost(maxRetryCount: 2);

        host.Faults.FailBeforeSave(times: 10);

        using var context = host.NewContext();

        var attempts = 0;

        var act = async () => await context.ExecuteTransactionalAsync<int>(token =>
        {
            attempts++;
            host.Faults.ThrowIfFailingBeforeSave();
            return Task.FromResult(0);
        });

        // EF's own budget, enforced by EF. The platform adds no retry logic of its own and this is
        // the assertion that says so: the failure that surfaces is the strategy's, carrying the
        // real cause inside it.
        var failure = await act.Should().ThrowAsync<RetryLimitExceededException>();

        failure.Which.InnerException.Should().BeOfType<TransientTestException>();

        attempts.Should().Be(3, "the first attempt plus two retries, then it gives up");
    }
}

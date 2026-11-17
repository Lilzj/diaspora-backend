using Diaspora.Domain.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// BBL-002. A write refused by a unique index leaves the persistence layer as
/// <see cref="DuplicateRecordException"/> rather than as EF Core's <see cref="DbUpdateException"/>.
/// <para>
/// Three of these tests exist to prove the translation is <em>narrow</em>, which is the only part
/// that could go wrong quietly. Catching too much here would turn a lost update into "that already
/// exists" and a missing required column into a 409, and both would look like a passing suite: the
/// one test that proves a duplicate becomes a duplicate would still be green.
/// </para>
/// <para>
/// The real unique indexes are exercised elsewhere, on production tables —
/// <c>OutboxTransactionalityTests.The_same_event_cannot_be_enqueued_twice</c> for the outbox's
/// message-id index, and the ad-placement and job-application indexes in the integration suite.
/// What these tests add is the discrimination between exception families, which needs a context
/// whose model declares a concurrency token and a required column.
/// </para>
/// </summary>
public sealed class DuplicateRecordTranslationTests
{
    /// <summary>
    /// A second insert of a row that is already there. Reached the way the platform's own dispatch
    /// loop reaches it — a save that does not accept its changes leaves the entity <c>Added</c>, so
    /// the next save re-sends the insert — because that is a real path rather than a contrived one.
    /// </summary>
    [Fact]
    public void A_write_the_database_already_has_is_a_duplicate()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-DUP"));

        context.SaveChanges(acceptAllChangesOnSuccess: false);

        context.Invoking(ctx => ctx.SaveChanges(acceptAllChangesOnSuccess: false))
            .Should().Throw<DuplicateRecordException>(
                "a unique violation is a fact about the data, and the application layer is not " +
                "allowed to reference the library that noticed it");
    }

    /// <summary>
    /// The constraint name is the most useful thing a developer can be told and the least safe
    /// thing to tell a caller, so it travels in the inner exception rather than in the message.
    /// <c>TransactionBehavior</c> logs the exception object on rollback, which is how it survives.
    /// </summary>
    [Fact]
    public void The_refusal_still_carries_what_the_database_said()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-INNER"));
        context.SaveChanges(acceptAllChangesOnSuccess: false);

        var refusal = Assert.Throws<DuplicateRecordException>(
            () => context.SaveChanges(acceptAllChangesOnSuccess: false));

        refusal.Message.Should().NotContain(
            "orders", "the response must not name a table or its columns");

        refusal.InnerException.Should().BeOfType<DbUpdateException>();

        Chain(refusal).Should().Contain(
            message => message.Contains("UNIQUE constraint failed", StringComparison.Ordinal),
            "the detail a developer needs is still reachable from the exception");
    }

    /// <summary>
    /// <b>The ordering guarantee.</b> <see cref="DbUpdateConcurrencyException"/> derives from
    /// <see cref="DbUpdateException"/>, so an arm placed above the concurrency arm would report
    /// every lost update as a duplicate — and tell the caller not to retry something a retry would
    /// have fixed. Nothing but this test stands between those two arms staying in order.
    /// </summary>
    [Fact]
    public void A_lost_update_is_still_a_concurrency_conflict()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-RACE"));

        using var stale = host.NewContext();
        var staleOrder = SoftDeleteTestHost.Load(stale, id)!;

        using (var other = host.NewContext())
        {
            var current = SoftDeleteTestHost.Load(other, id)!;

            current.Rename("ORD-RACE-REV");
            current.Touch();

            other.SaveChanges();
        }

        staleOrder.Rename("ORD-RACE-MINE");
        staleOrder.Touch();

        // **What this proves, measured rather than assumed.** Two things in PersistAsync keep a
        // lost update out of the duplicate arm, and they are redundant: the concurrency arm sits
        // first, and the predicate matches on text that EF's concurrency message — which is about
        // how many rows an operation affected — does not contain. Each was broken on its own and
        // this test stayed green both times. Breaking both at once fails it, along with
        // SoftDeleteCascadeTests.Optimistic_concurrency_still_applies_to_a_soft_delete and
        // TransactionCompatibilityTests.A_concurrency_conflict_is_not_retried_and_still_translates.
        //
        // So no behavioural test can pin the arm order by itself: the filter makes the order
        // unobservable. That is what
        // DuplicateRecordArchitectureTests.The_concurrency_arm_is_tried_before_the_duplicate_arm
        // is for, and why it reads the syntax instead.
        stale.Invoking(context => context.SaveChanges())
            .Should().Throw<ConcurrencyConflictException>();
    }

    /// <summary>
    /// A required column with no value is a <see cref="DbUpdateException"/> too, and it is not a
    /// duplicate. It must still escape untranslated: there is no 409 that describes it, and
    /// answering one would tell the caller to stop retrying a request that is simply wrong.
    /// </summary>
    [Fact]
    public void A_failure_that_is_not_a_duplicate_is_left_alone()
    {
        using var host = new SoftDeleteTestHost();
        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create(null!));

        // Assert.Throws is exact-type in xUnit, so this already refuses a translated subclass as
        // well as anything the filter widened into.
        var refusal = Assert.Throws<DbUpdateException>(() => context.SaveChanges());

        Chain(refusal).Should().Contain(
            message => message.Contains("NOT NULL constraint failed", StringComparison.Ordinal),
            "the test is only meaningful if the save failed for the reason it was meant to");
    }

    private static List<string> Chain(Exception exception)
    {
        var messages = new List<string>();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return messages;
    }
}

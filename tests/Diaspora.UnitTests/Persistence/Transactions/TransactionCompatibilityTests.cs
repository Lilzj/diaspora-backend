using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.UnitTests.Platform.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// B1.0.2 against the two phases it sits between: B1.0.1's soft-delete cascade repair and B1.1's
/// transactional outbox.
/// <para>
/// Both were built on the assumption that <c>ExecuteTransactionalAsync</c> commits or rolls back as
/// a unit. B1.0.2 changed how that commit is reached — a receipt written inside the transaction, a
/// verification delegate, a guard on replay — and the whole value of the change depends on those
/// two guarantees surviving it exactly as they were.
/// </para>
/// </summary>
public sealed class TransactionCompatibilityTests
{
    // ── B1.1: the outbox ────────────────────────────────────────────────

    /// <summary>
    /// The outbox row and the aggregate still commit together, and a verified commit leaves exactly
    /// one of each. A replay here would have produced two aggregates and two messages — and the
    /// second message would have been delivered to every consumer as a genuine second event.
    /// </summary>
    [Fact]
    public async Task An_outbox_message_and_its_aggregate_survive_a_lost_acknowledgement_exactly_once()
    {
        using var host = new TransactionTestHost();

        host.Faults.FailCommitAcknowledgement();

        using var context = host.NewContext();

        await context.ExecuteTransactionalAsync(async token =>
        {
            context.Add(SoftDeleteTestOrder.Create("ORD-OUTBOX"));

            host.OutboxWriter(context).Enqueue(OutboxMessageBuilder.NewEvent("committed once"));

            return await context.SaveChangesAsync(token);
        });

        host.CountOrders().Should().Be(1);
        host.CountOutboxMessages().Should().Be(1, "a duplicated message is a duplicated notification");
    }

    /// <summary>
    /// And the rollback half: a refused replay takes the outbox row with the aggregate. An event
    /// announcing something that did not happen is worse than a missing one.
    /// </summary>
    [Fact]
    public async Task A_refused_replay_leaves_no_outbox_message_behind()
    {
        using var host = new TransactionTestHost();

        host.Faults.FailAfterSave();

        using var context = host.NewContext();

        var act = async () => await context.ExecuteTransactionalAsync(async token =>
        {
            context.Add(SoftDeleteTestOrder.Create("ORD-ROLLBACK"));

            host.OutboxWriter(context).Enqueue(OutboxMessageBuilder.NewEvent("never happened"));

            var written = await context.SaveChangesAsync(token);

            host.Faults.ThrowIfFailingAfterSave();

            return written;
        });

        await act.Should().ThrowAsync<TransientTransactionFailureException>();

        host.CountOrders().Should().Be(0);
        host.CountOutboxMessages().Should().Be(0);
    }

    // ── B1.0.1: soft delete over owned types ────────────────────────────

    /// <summary>
    /// The soft-delete cascade repair still runs, and its work still commits, on the verified path.
    /// The failure it prevents — owned value objects hollowed out to NULL on a soft delete — is
    /// silent, so it is worth re-proving under every execution mode rather than assuming.
    /// </summary>
    [Fact]
    public async Task Soft_delete_preserves_owned_value_objects_on_the_verified_path()
    {
        using var host = new TransactionTestHost();

        SoftDeleteTestOrderId id;

        using (var seed = host.NewContext())
        {
            var order = SoftDeleteTestOrder.Create("ORD-SOFT")
                .WithLines(("SKU-1", 2), ("SKU-2", 1));

            seed.Add(order);
            await seed.SaveChangesAsync();
            id = order.Id;
        }

        // Injected after the seed, deliberately. This host has no event dispatcher, so its saves
        // take the plain path and EF manages their transactions itself — and EF's own SaveChanges
        // retry has exactly the hazard this phase fixes, with no verification of its own. In a
        // configured host that path does not exist: a dispatcher is always registered, so every
        // top-level save routes through ExecuteTransactionalAsync. See
        // TransactionReliabilityTests.Every_top_level_save_opens_a_platform_transaction.
        host.Faults.FailCommitAcknowledgement();

        using (var context = host.NewContext())
        {
            await context.ExecuteTransactionalAsync(async token =>
            {
                var order = SoftDeleteTestHost.Load(context, id)!;
                context.Remove(order);
                return await context.SaveChangesAsync(token);
            });
        }

        using var verification = host.NewContext();

        var deleted = SoftDeleteTestHost.Load(verification, id)!;

        deleted.IsDeleted.Should().BeTrue();
        deleted.ShippingAddress.City.Should().Be("Port Louis");
        deleted.ShippingAddress.Postcode.Should().Be("11324");
        deleted.Lines.Should().NotBeEmpty("the owned collection must survive the soft delete");
    }

    // ── Concurrency ─────────────────────────────────────────────────────

    /// <summary>
    /// A lost optimistic-concurrency race is a domain fact, not a transient failure. It must not be
    /// retried — a retry would either lose the race again or, worse, win it and overwrite the
    /// change it lost to — and it must still surface as the platform's own exception rather than
    /// EF's.
    /// </summary>
    [Fact]
    public async Task A_concurrency_conflict_is_not_retried_and_still_translates()
    {
        using var host = new TransactionTestHost();

        SoftDeleteTestOrderId id;

        using (var seed = host.NewContext())
        {
            var order = SoftDeleteTestOrder.Create("ORD-RACE");
            seed.Add(order);
            await seed.SaveChangesAsync();
            id = order.Id;
        }

        using var first = host.NewContext();

        var mine = SoftDeleteTestHost.Load(first, id)!;

        // Someone else gets there first and moves the concurrency token on, exactly as the
        // B1.0.1 suite stages the same race.
        using (var second = host.NewContext())
        {
            var theirs = SoftDeleteTestHost.Load(second, id)!;

            theirs.Rename("ORD-RACE-REV");
            theirs.Touch();

            await second.SaveChangesAsync();
        }

        var attempts = 0;

        var act = async () => await first.ExecuteTransactionalAsync(async token =>
        {
            attempts++;
            first.Remove(mine);
            return await first.SaveChangesAsync(token);
        });

        await act.Should().ThrowAsync<ConcurrencyConflictException>();

        attempts.Should().Be(1, "losing a race is not a transient failure");
    }

    // ── The dispatch loop ───────────────────────────────────────────────

    /// <summary>
    /// A multi-round save — the shape every command that publishes an integration event takes —
    /// still commits as one transaction and writes one receipt for the whole of it.
    /// </summary>
    [Fact]
    public async Task A_multi_round_save_commits_once_and_writes_one_receipt()
    {
        using var host = new TransactionTestHost(
            eventDispatcher: new RecordingDomainEventDispatcher());

        using var context = host.NewContext();

        context.Add(SoftDeleteTestOrder.Create("ORD-ROUNDS"));

        await context.SaveChangesAsync();

        host.CountOrders().Should().Be(1);
        host.CountReceipts().Should().Be(
            1, "the receipt marks the transaction, not each save inside it");
    }
}

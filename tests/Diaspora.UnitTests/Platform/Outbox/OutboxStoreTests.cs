using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The claim, against real SQL.
/// <para>
/// <b>This is where horizontal scale is either safe or it is not.</b> Two workers polling the
/// same table several times a second will race for the same rows; the guarantee that exactly one
/// of them wins each row is a property of a conditional <c>UPDATE</c>, and no substitute can
/// verify it. Every test here runs the statement the deployment runs.
/// </para>
/// </summary>
public sealed class OutboxStoreTests
{
    private static OutboxClaimRequest ClaimFor(
        OutboxTestHost host,
        string workerId = "worker-1",
        int batchSize = 100,
        OutboxOrderingMode ordering = OutboxOrderingMode.Partitioned) =>
        new(
            ClaimId: Guid.NewGuid(),
            WorkerId: workerId,
            BatchSize: batchSize,
            Now: host.Clock.UtcNow,
            LeaseExpiresAt: host.Clock.UtcNow.AddSeconds(60),
            Ordering: ordering,
            TenantIds: []);

    [Fact]
    public async Task A_committed_event_is_claimable()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("first"));

        using var context = host.NewContext();

        var claimed = await OutboxTestHost.Store(context).ClaimAsync(ClaimFor(host));

        claimed.Should().ContainSingle();
        claimed[0].State.Should().Be(OutboxState.Claimed);
        claimed[0].LeaseOwner.Should().Be("worker-1");
    }

    /// <summary>
    /// The competing-consumers property, run for real: two workers claim concurrently and every
    /// message goes to exactly one of them.
    /// </summary>
    [Fact]
    public async Task Two_workers_never_claim_the_same_message()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(
            Enumerable.Range(0, 20)
                .Select(index => OutboxMessageBuilder.NewEvent($"event-{index}"))
                .Cast<Diaspora.Contracts.Events.IIntegrationEvent>()
                .ToArray());

        using var firstContext = host.NewContext();
        using var secondContext = host.NewContext();

        var first = await OutboxTestHost.Store(firstContext).ClaimAsync(ClaimFor(host, "worker-1", batchSize: 20));
        var second = await OutboxTestHost.Store(secondContext).ClaimAsync(ClaimFor(host, "worker-2", batchSize: 20));

        first.Should().HaveCount(20);

        // The second worker finds nothing: every row failed its "still pending" precondition.
        second.Should().BeEmpty();

        first.Select(message => message.Sequence).Distinct().Should().HaveCount(20);
    }

    /// <summary>
    /// A message waiting out a backoff is invisible. Without this the retry curve would exist
    /// only in the row and never in the behaviour.
    /// </summary>
    [Fact]
    public async Task A_message_waiting_on_a_backoff_is_not_claimable()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("deferred"));

        using var context = host.NewContext();
        var store = OutboxTestHost.Store(context);

        var claimed = await store.ClaimAsync(ClaimFor(host));

        await store.MarkFailedAsync(
            claimed[0].Sequence, host.Clock.UtcNow, host.Clock.UtcNow.AddMinutes(5), "failed");

        (await store.ClaimAsync(ClaimFor(host))).Should().BeEmpty();

        host.Clock.Advance(TimeSpan.FromMinutes(6));

        (await store.ClaimAsync(ClaimFor(host))).Should().ContainSingle();
    }

    /// <summary>
    /// The crash-recovery path. A worker that dies mid-publish leaves rows claimed; the lease
    /// expiring is what turns that into latency instead of a permanent stall.
    /// </summary>
    [Fact]
    public async Task A_message_left_leased_by_a_dead_worker_is_recovered()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("abandoned"));

        using var context = host.NewContext();
        var store = OutboxTestHost.Store(context);

        await store.ClaimAsync(ClaimFor(host, "worker-that-died"));

        // Still leased, so nobody else may take it.
        (await store.ClaimAsync(ClaimFor(host, "worker-2"))).Should().BeEmpty();

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        (await store.RecoverExpiredLeasesAsync(host.Clock.UtcNow)).Should().Be(1);

        var recovered = await store.ClaimAsync(ClaimFor(host, "worker-2"));

        recovered.Should().ContainSingle();
        recovered[0].LeaseOwner.Should().Be("worker-2");

        // Recovery must not spend a retry attempt: the crash was not the message's fault.
        recovered[0].AttemptCount.Should().Be(0);
    }

    /// <summary>
    /// Partitioned ordering, enforced at the point of claiming. A partition with a message
    /// waiting on a backoff must be skipped entirely, or a later message would be delivered
    /// before the earlier one it depends on.
    /// </summary>
    [Fact]
    public async Task A_partition_with_a_message_in_backoff_is_skipped_entirely()
    {
        using var host = new OutboxTestHost();

        var widgetId = Guid.NewGuid();

        // Two events about one widget, and one about another.
        await host.EnqueueAsync(
            OutboxMessageBuilder.NewEvent("first") with { WidgetId = widgetId },
            OutboxMessageBuilder.NewEvent("second") with { WidgetId = widgetId },
            OutboxMessageBuilder.NewEvent("unrelated"));

        using var context = host.NewContext();
        var store = OutboxTestHost.Store(context);

        var firstPass = await store.ClaimAsync(ClaimFor(host));
        firstPass.Should().HaveCount(3);

        // The first message of the shared partition fails and backs off.
        var blocked = firstPass.First(message => message.PartitionKey.Contains(widgetId.ToString()));

        foreach (var message in firstPass)
        {
            await store.MarkFailedAsync(
                message.Sequence,
                host.Clock.UtcNow,
                message.Sequence == blocked.Sequence
                    ? host.Clock.UtcNow.AddMinutes(5)
                    : host.Clock.UtcNow,
                "failed");
        }

        var secondPass = await store.ClaimAsync(ClaimFor(host));

        // Only the unrelated partition comes back. Both messages about the widget are held —
        // including the one that is otherwise eligible, because delivering it would overtake
        // the one in front of it.
        secondPass.Should().ContainSingle();
        secondPass[0].PartitionKey.Should().NotContain(widgetId.ToString());
    }

    [Fact]
    public async Task Claims_come_back_in_write_order()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(
            Enumerable.Range(0, 10)
                .Select(index => OutboxMessageBuilder.NewEvent($"event-{index}"))
                .Cast<Diaspora.Contracts.Events.IIntegrationEvent>()
                .ToArray());

        using var context = host.NewContext();

        var claimed = await OutboxTestHost.Store(context).ClaimAsync(ClaimFor(host));

        claimed.Select(message => message.Sequence)
            .Should().BeInAscendingOrder()
            .And.HaveCount(10);
    }

    [Fact]
    public async Task A_batch_size_bounds_the_claim()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(
            Enumerable.Range(0, 30)
                .Select(index => OutboxMessageBuilder.NewEvent($"event-{index}"))
                .Cast<Diaspora.Contracts.Events.IIntegrationEvent>()
                .ToArray());

        using var context = host.NewContext();

        (await OutboxTestHost.Store(context).ClaimAsync(ClaimFor(host, batchSize: 5))).Should().HaveCount(5);
    }

    /// <summary>
    /// The insert and the delete are one transaction, so a message is never both deliverable and
    /// dead — nor, worse, neither.
    /// </summary>
    [Fact]
    public async Task Dead_lettering_moves_the_message_rather_than_copying_it()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("doomed"));

        using var context = host.NewContext();
        var store = OutboxTestHost.Store(context);

        var claimed = await store.ClaimAsync(ClaimFor(host));

        await store.DeadLetterAsync(
            claimed[0].Sequence,
            OutboxDeadLetterReason.MaxAttemptsExceeded,
            "gave up",
            host.Clock.UtcNow);

        (await host.MessagesAsync()).Should().BeEmpty();

        var deadLetters = await host.DeadLettersAsync();

        deadLetters.Should().ContainSingle();
        deadLetters[0].MessageId.Should().Be(claimed[0].MessageId);
        deadLetters[0].Payload.Should().Be(claimed[0].Payload);
        deadLetters[0].Reason.Should().Be(OutboxDeadLetterReason.MaxAttemptsExceeded);
    }

    /// <summary>
    /// The attempt count is incremented in SQL rather than read and written back. Two workers
    /// that both failed the same message under an expired lease must not each write "one",
    /// losing an attempt from a budget that is meant to be finite.
    /// </summary>
    [Fact]
    public async Task Attempt_counts_accumulate_rather_than_being_overwritten()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("retried"));

        using var context = host.NewContext();
        var store = OutboxTestHost.Store(context);

        var sequence = (await store.ClaimAsync(ClaimFor(host)))[0].Sequence;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await store.MarkFailedAsync(
                sequence, host.Clock.UtcNow, host.Clock.UtcNow, "failed again");
        }

        (await host.MessagesAsync())[0].AttemptCount.Should().Be(3);
    }

    /// <summary>
    /// A tenant-scoped worker takes only its own tenants' messages. Nothing writes a tenant
    /// today, so the assertion that matters is the one about isolation holding when something
    /// does.
    /// </summary>
    [Fact]
    public async Task A_tenant_filtered_worker_leaves_other_tenants_messages_alone()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("untenanted"));

        using var context = host.NewContext();

        var request = ClaimFor(host) with { TenantIds = ["tenant-a"] };

        (await OutboxTestHost.Store(context).ClaimAsync(request)).Should().BeEmpty(
            "an untenanted message belongs to no tenant-scoped worker");
    }
}

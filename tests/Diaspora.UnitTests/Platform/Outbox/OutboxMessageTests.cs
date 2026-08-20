using Diaspora.Platform.Messaging.Outbox.Model;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The message's own state machine.
/// <para>
/// Every transition releases the lease. That is the invariant worth testing directly, because
/// the failure it prevents is silent: a message left marked as leased by a worker that has moved
/// on is invisible to every other worker until the lease expires, and a message that keeps its
/// claim identifier after being released can be read back by a stale claim.
/// </para>
/// </summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    private static OutboxMessage NewMessage() => OutboxMessage.Create(
        messageId: Guid.NewGuid(),
        eventType: "diaspora.test.event",
        eventVersion: 1,
        payload: "{}",
        metadata: null,
        module: "Tests",
        partitionKey: "WidgetId:1",
        tenantId: null,
        correlationId: "corr-1",
        causationId: null,
        occurredAt: Now,
        enqueuedAt: Now);

    [Fact]
    public void A_new_message_is_pending_and_immediately_eligible()
    {
        var message = NewMessage();

        message.State.Should().Be(OutboxState.Pending);
        message.AttemptCount.Should().Be(0);
        message.NextAttemptAt.Should().Be(Now);
        message.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Delivery_records_the_attempt_and_releases_the_lease()
    {
        var message = NewMessage();

        message.MarkDelivered(Now.AddSeconds(5));

        message.State.Should().Be(OutboxState.Delivered);
        message.DeliveredAt.Should().Be(Now.AddSeconds(5));
        message.AttemptCount.Should().Be(1);
        message.NextAttemptAt.Should().BeNull();
        message.LeaseOwner.Should().BeNull();
        message.LeaseExpiresAt.Should().BeNull();
        message.ClaimId.Should().BeNull();
    }

    [Fact]
    public void A_failure_schedules_the_next_attempt_and_releases_the_lease()
    {
        var message = NewMessage();

        message.MarkFailed(Now, Now.AddSeconds(30), "the consumer refused");

        message.State.Should().Be(OutboxState.Pending);
        message.AttemptCount.Should().Be(1);
        message.NextAttemptAt.Should().Be(Now.AddSeconds(30));
        message.LastError.Should().Be("the consumer refused");
        message.LeaseExpiresAt.Should().BeNull();
        message.ClaimId.Should().BeNull();
    }

    /// <summary>
    /// Errors are stored for a human reading a triage screen. A stack trace from a deeply nested
    /// handler runs to tens of kilobytes, and one bad deployment would otherwise turn the outbox
    /// into a log sink — and overflow the column while it was at it.
    /// </summary>
    [Fact]
    public void An_enormous_error_is_truncated_to_fit_its_column()
    {
        var message = NewMessage();

        message.MarkFailed(Now, Now, new string('e', OutboxFieldLimits.Error * 3));

        message.LastError.Should().HaveLength(OutboxFieldLimits.Error);
    }

    [Fact]
    public void A_dead_letter_carries_the_full_payload_and_the_attempt_that_ended_it()
    {
        var message = NewMessage();

        message.MarkFailed(Now, Now, "first failure");

        var deadLetter = message.ToDeadLetter(
            OutboxDeadLetterReason.MaxAttemptsExceeded, "final failure", Now.AddMinutes(40));

        deadLetter.MessageId.Should().Be(message.MessageId);
        deadLetter.EventType.Should().Be(message.EventType);
        deadLetter.Payload.Should().Be(message.Payload);
        deadLetter.CorrelationId.Should().Be("corr-1");
        deadLetter.Reason.Should().Be(OutboxDeadLetterReason.MaxAttemptsExceeded);
        deadLetter.DeadLetteredAt.Should().Be(Now.AddMinutes(40));

        // One failure recorded plus the attempt that dead-lettered it.
        deadLetter.AttemptCount.Should().Be(2);
    }

    /// <summary>
    /// The property a replay depends on: consumers are told to deduplicate on the message
    /// identifier, so a replay must present the same one or every idempotent consumer will treat
    /// it as new.
    /// </summary>
    [Fact]
    public void A_replay_preserves_the_original_message_identifier()
    {
        var original = NewMessage();

        var deadLetter = original.ToDeadLetter(
            OutboxDeadLetterReason.Poison, "unreadable", Now);

        var replayed = OutboxMessage.Replay(deadLetter, Now.AddDays(1));

        replayed.MessageId.Should().Be(original.MessageId);
        replayed.Payload.Should().Be(original.Payload);
        replayed.PartitionKey.Should().Be(original.PartitionKey);
        replayed.OccurredAt.Should().Be(original.OccurredAt);
    }

    /// <summary>
    /// A replay resets the retry budget. Without that, a dead letter put back after a dependency
    /// is fixed would arrive with its budget already spent and dead-letter again on the first
    /// hiccup.
    /// </summary>
    [Fact]
    public void A_replay_starts_with_a_fresh_retry_budget()
    {
        var original = NewMessage();

        original.MarkFailed(Now, Now, "failure");
        original.MarkFailed(Now, Now, "failure");

        var replayed = OutboxMessage.Replay(
            original.ToDeadLetter(OutboxDeadLetterReason.MaxAttemptsExceeded, "final", Now),
            Now.AddDays(1));

        replayed.AttemptCount.Should().Be(0);
        replayed.State.Should().Be(OutboxState.Pending);
        replayed.EnqueuedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Releasing_a_message_makes_it_immediately_claimable_again()
    {
        var message = NewMessage();

        message.Release(Now.AddMinutes(1));

        message.State.Should().Be(OutboxState.Pending);
        message.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        message.LeaseOwner.Should().BeNull();
    }

    /// <summary>
    /// An outbox row that raised domain events would enqueue a message describing its own
    /// creation, which would enqueue another, forever. A soft-deletable one would be filtered
    /// out of the dispatcher's own query while still occupying the table. Neither is a
    /// hypothetical: both interfaces are applied automatically by convention elsewhere in the
    /// persistence layer.
    /// </summary>
    [Fact]
    public void The_outbox_tables_are_not_aggregates()
    {
        foreach (var type in new[]
                 {
                     typeof(OutboxMessage), typeof(OutboxDeadLetter), typeof(OutboxDeliveryAttempt)
                 })
        {
            type.GetInterfaces().Select(contract => contract.Name)
                .Should().NotContain(["IHasDomainEvents", "ISoftDeletable", "IAuditableEntity"],
                    $"{type.Name} is infrastructure and must stay outside the aggregate pipeline");
        }
    }
}

using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;

namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>
/// What one channel is doing about one message.
/// <para>
/// <b>An owned entity, not a value object, and the difference is that this one changes.</b> A
/// delivery is created pending, moves to sending, may retry four times and ends delivered or failed
/// — a value object would have to be replaced on every one of those, and replacing an owned row in
/// EF Core is a delete and an insert that loses the identity an operator is watching.
/// </para>
/// <para>
/// <b>It never decides anything.</b> Whether a channel should be used at all is a preference
/// question answered before this exists; whether another attempt is worth making is a priority
/// question answered by <see cref="NotificationPriorityPolicy"/>. This records.
/// </para>
/// </summary>
public sealed class NotificationDelivery
{
    private NotificationDelivery(
        Guid deliveryId,
        NotificationChannelKind channel,
        DeliveryStatus status,
        DateTimeOffset deliverAfter,
        DateTimeOffset createdAt)
    {
        DeliveryId = deliveryId;
        Channel = channel;
        Status = status;
        DeliverAfter = deliverAfter;
        CreatedAt = createdAt;
        NextAttemptAt = deliverAfter;
    }

    private NotificationDelivery() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid DeliveryId { get; private set; }

    public NotificationChannelKind Channel { get; private set; }

    public DeliveryStatus Status { get; private set; }

    /// <summary>The earliest moment the channel may act. Later than creation for anything held.</summary>
    public DateTimeOffset DeliverAfter { get; private set; }

    /// <summary>When the dispatcher should next look at this row. Null once it is finished.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>The last machine code the channel reported. Null while nothing has failed.</summary>
    public string? ReasonCode { get; private set; }

    public string? ProviderMessageId { get; private set; }

    /// <summary>Set when this delivery was folded into a digest rather than sent.</summary>
    public Guid? DigestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsTerminal => DeliveryStatusTransitions.IsTerminal(Status);

    public bool IsRetryable => DeliveryStatusTransitions.IsRetryable(Status);

    public static NotificationDelivery Pending(
        Guid deliveryId,
        NotificationChannelKind channel,
        DateTimeOffset deliverAfter,
        DateTimeOffset createdAt) =>
        new(deliveryId, channel, DeliveryStatus.Pending, deliverAfter, createdAt);

    /// <summary>Held for a later moment — quiet hours, a schedule, a digest window.</summary>
    public bool Defer(DateTimeOffset until)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Deferred))
        {
            return false;
        }

        Status = DeliveryStatus.Deferred;
        DeliverAfter = until;
        NextAttemptAt = until;

        return true;
    }

    public bool BeginAttempt(DateTimeOffset now)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Sending))
        {
            return false;
        }

        Status = DeliveryStatus.Sending;
        AttemptCount++;
        LastAttemptAt = now;
        NextAttemptAt = null;

        return true;
    }

    public bool Succeed(string? providerMessageId, DateTimeOffset now)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Delivered))
        {
            return false;
        }

        Status = DeliveryStatus.Delivered;
        ProviderMessageId = providerMessageId;
        ReasonCode = null;
        CompletedAt = now;
        NextAttemptAt = null;

        return true;
    }

    /// <summary>
    /// Records a failed attempt and decides whether there will be another.
    /// <para>
    /// Returns true when the ladder has ended — permanently, or because the attempts ran out — so
    /// the aggregate knows whether to publish a failure. A retry schedules itself and says nothing.
    /// </para>
    /// </summary>
    public bool Fail(
        string reasonCode,
        bool isPermanent,
        NotificationPriority priority,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        ReasonCode = reasonCode;
        LastAttemptAt = now;

        var exhausted = isPermanent
            || AttemptCount >= NotificationPriorityPolicy.MaxAttempts(priority)
            || AttemptCount >= NotificationLimits.MaxDeliveryAttempts;

        if (exhausted)
        {
            Status = DeliveryStatus.Failed;
            CompletedAt = now;
            NextAttemptAt = null;

            return true;
        }

        Status = DeliveryStatus.Retrying;
        NextAttemptAt = now.Add(NotificationPriorityPolicy.BackoffFor(priority, AttemptCount + 1));

        return false;
    }

    /// <summary>
    /// The member's own preference stopped this. Terminal, and deliberately not retryable — an
    /// operations console must not be able to override a stated preference.
    /// </summary>
    public bool Suppress(string reasonCode, DateTimeOffset now)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Suppressed))
        {
            return false;
        }

        Status = DeliveryStatus.Suppressed;
        ReasonCode = reasonCode;
        CompletedAt = now;
        NextAttemptAt = null;

        return true;
    }

    /// <summary>Folded into a digest. The digest's own delivery carries it from here.</summary>
    public bool Digest(Guid digestId, DateTimeOffset now)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Digested))
        {
            return false;
        }

        Status = DeliveryStatus.Digested;
        DigestId = digestId;
        CompletedAt = now;
        NextAttemptAt = null;

        return true;
    }

    /// <summary>
    /// No implementation is registered for this channel kind.
    /// <para>
    /// Recorded rather than skipped, because "we have no SMS provider" and "we chose not to send"
    /// look identical in a support conversation and are entirely different problems.
    /// </para>
    /// </summary>
    public bool MarkUnsupported(DateTimeOffset now)
    {
        if (!DeliveryStatusTransitions.CanTransition(Status, DeliveryStatus.Unsupported))
        {
            return false;
        }

        Status = DeliveryStatus.Unsupported;
        ReasonCode = "ChannelNotConfigured";
        CompletedAt = now;
        NextAttemptAt = null;

        return true;
    }

    /// <summary>
    /// An operator asked for one more try.
    /// <para>
    /// Resets the row to pending rather than incrementing from where it stopped, so the attempt
    /// budget applies to the manual attempt too. Refused for anything not genuinely failed.
    /// </para>
    /// </summary>
    public bool Requeue(DateTimeOffset now)
    {
        if (!IsRetryable)
        {
            return false;
        }

        Status = DeliveryStatus.Pending;
        DeliverAfter = now;
        NextAttemptAt = now;
        CompletedAt = null;

        return true;
    }
}

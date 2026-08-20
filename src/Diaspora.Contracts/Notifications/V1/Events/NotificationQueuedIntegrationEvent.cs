using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A notification was accepted for delivery on one channel.
/// <para>
/// One per channel, not one per notification. A message going in-app and by push queues twice, and
/// the two can succeed and fail independently — which is the whole reason delivery is modelled per
/// channel rather than as a status on the message.
/// </para>
/// </summary>
public sealed record NotificationQueuedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    /// <summary><c>InApp</c>, <c>Push</c>, <c>Email</c>, <c>Sms</c>, <c>WhatsApp</c>, <c>Voice</c>, <c>Webhook</c>.</summary>
    public required string Channel { get; init; }

    public required string CategoryKey { get; init; }

    public required string Priority { get; init; }

    /// <summary>
    /// When the channel may act. Later than <see cref="QueuedAt"/> for anything held by quiet
    /// hours, a digest window or a scheduled reminder.
    /// </summary>
    public required DateTimeOffset DeliverAfter { get; init; }

    public required DateTimeOffset QueuedAt { get; init; }
}

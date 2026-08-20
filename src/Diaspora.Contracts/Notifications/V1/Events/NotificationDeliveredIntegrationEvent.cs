using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A channel accepted the message and reported success.
/// <para>
/// Delivered means the transport took it, not that a human saw it. The distance between this and
/// <see cref="NotificationOpenedIntegrationEvent"/> is the number every channel is judged on.
/// </para>
/// </summary>
public sealed record NotificationDeliveredIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required string Channel { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>How many attempts it took, including this one. One is the normal case.</summary>
    public required int Attempt { get; init; }

    /// <summary>The transport's own receipt, when it gave one. Never interpreted here.</summary>
    public string? ProviderMessageId { get; init; }

    public required DateTimeOffset DeliveredAt { get; init; }
}

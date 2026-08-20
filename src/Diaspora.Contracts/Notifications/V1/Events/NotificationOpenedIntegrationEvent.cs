using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>A member read the message. Raised once; a second read is not a second open.</summary>
public sealed record NotificationOpenedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required string TemplateKey { get; init; }

    /// <summary>Where it was opened. <c>InApp</c> for the inbox, the channel name otherwise.</summary>
    public required string Channel { get; init; }

    /// <summary>Minutes from creation to first read. The responsiveness measure.</summary>
    public required double MinutesToOpen { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }
}

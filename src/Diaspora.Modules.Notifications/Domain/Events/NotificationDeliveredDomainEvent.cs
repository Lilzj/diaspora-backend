using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>A channel accepted the message.</summary>
public sealed record NotificationDeliveredDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationChannelKind Channel { get; init; }

    public required int Attempt { get; init; }

    public string? ProviderMessageId { get; init; }

    public required DateTimeOffset DeliveredAt { get; init; }
}

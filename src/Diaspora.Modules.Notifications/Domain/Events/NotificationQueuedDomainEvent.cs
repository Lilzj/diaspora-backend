using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>One channel was asked to carry the message.</summary>
public sealed record NotificationQueuedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationPriority Priority { get; init; }

    public required NotificationChannelKind Channel { get; init; }

    public required DateTimeOffset DeliverAfter { get; init; }

    public required DateTimeOffset QueuedAt { get; init; }
}

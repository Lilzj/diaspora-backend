using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>A member read the message. Raised once per notification, ever.</summary>
public sealed record NotificationOpenedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required string TemplateKey { get; init; }

    public required string Channel { get; init; }

    public required double MinutesToOpen { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }
}

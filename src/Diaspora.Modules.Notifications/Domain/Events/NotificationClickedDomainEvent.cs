using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>A member followed the message somewhere.</summary>
public sealed record NotificationClickedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required string TemplateKey { get; init; }

    public string? TargetRoute { get; init; }

    public string? ActionKey { get; init; }

    public required DateTimeOffset ClickedAt { get; init; }
}

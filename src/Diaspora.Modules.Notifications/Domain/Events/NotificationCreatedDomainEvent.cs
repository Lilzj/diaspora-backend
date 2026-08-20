using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A message was addressed to a member.
/// <para>
/// Internal. The relay turns it into the published contract; the inbox counters are settled from it
/// in the same unit of work, which is what keeps the badge and the message atomic.
/// </para>
/// </summary>
public sealed record NotificationCreatedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationPriority Priority { get; init; }

    public required string TemplateKey { get; init; }

    public string? RuleKey { get; init; }

    public required string SourceModule { get; init; }

    public string? SourceEventName { get; init; }

    public string? SubjectId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

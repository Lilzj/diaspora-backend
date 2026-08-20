using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A message left the inbox.
/// <para>
/// <see cref="WasUnread"/> is on the event rather than read back afterwards, because by the time a
/// handler runs the aggregate has already been changed and the counter it needs to move can no
/// longer be derived. Every counter defect this module could have had is a variation on that.
/// </para>
/// </summary>
public sealed record NotificationArchivedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required ArchiveReason Reason { get; init; }

    public required bool ByRecipient { get; init; }

    /// <summary>Whether it still counted against the badge when it was filed.</summary>
    public required bool WasUnread { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}

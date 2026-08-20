using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A message was soft-deleted.
/// <para>
/// Never published. A deletion is the member asking for something to be gone, and announcing it on
/// a bus every module reads would be the exact opposite of honouring that. It exists so the inbox
/// counters can be settled inside the same transaction.
/// </para>
/// </summary>
public sealed record NotificationDeletedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required bool WasUnread { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}

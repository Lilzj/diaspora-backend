using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member deleted a message.
/// <para>
/// Soft, so the delivery record survives an abuse investigation, and never published — a deletion is
/// somebody asking for something to be gone, and announcing it on a bus every module reads would be
/// the opposite of honouring that.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Removes the notification and settles the member's counters together.")]
[Auditable]
public sealed record DeleteNotificationCommand : ICommand
{
    public required Guid NotificationId { get; init; }
}

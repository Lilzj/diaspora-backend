using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member brought a filed message back.
/// <para>
/// The brief calls this "restore notification". It is the un-archive, not the un-delete: a
/// soft-deleted message stays deleted, which is what the member asked for. The two are separate
/// operations on separate endpoints, because a codebase where they share a name is one where a
/// restore eventually calls the wrong one.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's counters together.")]
[Auditable]
public sealed record RestoreNotificationCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }
}

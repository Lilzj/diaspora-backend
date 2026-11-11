using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>The member filed a message.</summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's counters together.")]
[Auditable]
public sealed record ArchiveNotificationCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }
}

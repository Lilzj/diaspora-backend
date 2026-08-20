using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>The member put a message back to unread. The one engagement fact they may undo.</summary>
[Authorize(Permission = NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's unread counters together.")]
public sealed record MarkNotificationUnreadCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>Stars or unstars a message. Independent of pinning, and usable on archived messages.</summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's starred counter together.")]
public sealed record StarNotificationCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }

    public required bool IsStarred { get; init; }
}

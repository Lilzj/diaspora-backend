using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>Pins or unpins a message. A pin is a position; a star is a bookmark.</summary>
[Authorize(Permission = NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's pinned counter together.")]
public sealed record PinNotificationCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }

    public required bool IsPinned { get; init; }
}

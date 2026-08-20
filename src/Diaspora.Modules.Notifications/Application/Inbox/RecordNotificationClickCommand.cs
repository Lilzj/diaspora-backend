using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>The member followed a notification somewhere.</summary>
[Authorize(Permission = NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification, the badge and the analytics together.")]
public sealed record RecordNotificationClickCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }

    public string? ActionKey { get; init; }

    public string? Channel { get; init; }
}

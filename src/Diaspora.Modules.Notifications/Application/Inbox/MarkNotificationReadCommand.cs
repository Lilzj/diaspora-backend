using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member read a message.
/// <para>
/// Transactional: the notification and the badge commit together. A badge that says one beside an
/// inbox that shows nothing is a member who stops trusting both.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's unread counters together.")]
public sealed record MarkNotificationReadCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }

    /// <summary>Where the member was. Recorded against the channel's open rate.</summary>
    public string? Channel { get; init; }
}

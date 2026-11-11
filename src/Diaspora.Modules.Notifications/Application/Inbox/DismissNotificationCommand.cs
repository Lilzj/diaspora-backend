using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member refused a message — swiped it away, tapped "not interested".
/// <para>
/// Distinct from archiving, and the distinction is what the analytics are for: filing something is
/// neutral, refusing it is feedback about the message. A dismiss rate is how a template's wording is
/// judged.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves the notification and the member's counters together.")]
public sealed record DismissNotificationCommand : ICommand<NotificationResponse>
{
    public required Guid NotificationId { get; init; }
}

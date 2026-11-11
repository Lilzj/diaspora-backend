using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// Everything read at once.
/// <para>
/// The client's "Mark all as read". Bounded per call and reported honestly: a member with fifty
/// thousand unread messages gets several calls rather than one request that holds a transaction open
/// across the lot.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxManage)]
[Transactional(Reason = "Moves every notification and the member's counters together.")]
[Auditable]
public sealed record MarkAllNotificationsReadCommand : ICommand<UnreadCountResponse>;

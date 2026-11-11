using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// One of the caller's own messages, with its delivery record.
/// <para>
/// A member may see their own deliveries — "we tried to email you and it bounced" is information they
/// are entitled to. What staff may see is the delivery record for anybody, through a different
/// endpoint that never returns the words.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxRead)]
public sealed record GetNotificationQuery : IQuery<NotificationDetailResponse>
{
    public required Guid NotificationId { get; init; }
}

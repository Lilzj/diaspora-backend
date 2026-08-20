using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The badge.
/// <para>
/// The most frequently requested number on the platform — every app foreground, by every member.
/// Answered from the maintained counter: one indexed row read, constant whatever the member's
/// history.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.InboxRead)]
public sealed record GetUnreadCountQuery : IQuery<UnreadCountResponse>;

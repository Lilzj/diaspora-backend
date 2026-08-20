using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Application.Digests;

/// <summary>The caller's digests, newest first.</summary>
[Authorize(Permission = NotificationPermissions.DigestsRead)]
public sealed record ListDigestsQuery : IQuery<CursorPagedResult<NotificationDigestResponse>>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

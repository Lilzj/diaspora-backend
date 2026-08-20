using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Digests;

/// <summary>The caller's digests.</summary>
public sealed class ListDigestsQueryHandler
    : IQueryHandler<ListDigestsQuery, CursorPagedResult<NotificationDigestResponse>>
{
    private readonly INotificationDigestRepository _digests;
    private readonly NotificationAuthorizationService _authorization;

    public ListDigestsQueryHandler(
        INotificationDigestRepository digests, NotificationAuthorizationService authorization)
    {
        _digests = digests;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<NotificationDigestResponse>>> Handle(
        ListDigestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<NotificationDigestResponse>>(caller.Error);
        }

        var page = await _digests.ListForUserAsync(
            caller.Value,
            query.Cursor,
            query.Limit ?? CursorPaginationRequest.DefaultLimit,
            cancellationToken);

        return Result.Success(CursorPagedResult<NotificationDigestResponse>.Create(
            page.Items.Select(DigestMapper.ToResponse).ToList(),
            page.NextCursor,
            page.HasMore));
    }
}

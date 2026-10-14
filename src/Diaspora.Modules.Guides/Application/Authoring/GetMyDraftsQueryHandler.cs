using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Serves the author's drafts, most recently edited first.
/// <para>
/// Both the author filter and the viewer are the caller: the filter is what selects their rows,
/// and the viewer is what permits drafts to appear at all. Neither is inferred from the other,
/// because a staff caller must not silently get everybody's drafts on their own screen.
/// </para>
/// </summary>
public sealed class GetMyDraftsQueryHandler
    : IQueryHandler<GetMyDraftsQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public GetMyDraftsQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        GetMyDraftsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<GuideCardResponse>>(caller.Error);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Draft],
            AuthorId = caller.Value,
            ViewerId = caller.Value,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.RecentlyUpdated,
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(page.Items, cancellationToken),
            page.NextCursor,
            page.HasMore));
    }
}

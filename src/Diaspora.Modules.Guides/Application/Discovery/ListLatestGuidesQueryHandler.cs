using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the newest page of published guides, straight from the repository's indexed
/// publication ordering — no ranking strategy runs, because "newest" is the one shelf where
/// reordering a page would be a lie about what it says on the tin.
/// </summary>
public sealed class ListLatestGuidesQueryHandler
    : IQueryHandler<ListLatestGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public ListLatestGuidesQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListLatestGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                ? null
                : query.CountryCode.Trim().ToUpperInvariant(),
            Language = string.IsNullOrWhiteSpace(query.Language)
                ? null
                : query.Language.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Newest,
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(page.Items, cancellationToken),
            page.NextCursor,
            page.HasMore));
    }
}

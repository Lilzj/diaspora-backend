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
/// Serves the featured shelf.
/// <para>
/// Published guides only, ordered newest first, and the audience fields are computed here and
/// handed to the filter — the repository turns them into predicates and no caller composes its
/// own visibility. Featuring a draft is impossible in the domain, so this list is public by
/// construction.
/// </para>
/// </summary>
public sealed class ListFeaturedGuidesQueryHandler
    : IQueryHandler<ListFeaturedGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public ListFeaturedGuidesQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListFeaturedGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            FeaturedOnly = true,
            CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                ? null
                : query.CountryCode.Trim().ToUpperInvariant(),
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

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves one country's shelf.
/// <para>
/// An unrecognised sort key falls back to newest rather than failing, per
/// <c>GuideWireNames.ParseSort</c>: a client that saved a sort a later deployment removed should
/// get a differently ordered browse, not an error page.
/// </para>
/// </summary>
public sealed class ListGuidesByCountryQueryHandler
    : IQueryHandler<ListGuidesByCountryQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideRankingStrategyRegistry _rankings;
    private readonly IClock _clock;

    public ListGuidesByCountryQueryHandler(
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideRankingStrategyRegistry rankings,
        IClock clock)
    {
        _guides = guides;
        _authorization = authorization;
        _rankings = rankings;
        _clock = clock;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListGuidesByCountryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var sort = GuideWireNames.ParseSort(query.Sort);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CountryCode = query.CountryCode?.Trim().ToUpperInvariant(),
            CategoryKey = string.IsNullOrWhiteSpace(query.CategoryKey)
                ? null
                : query.CategoryKey.Trim().ToLowerInvariant(),
            Language = string.IsNullOrWhiteSpace(query.Language)
                ? null
                : query.Language.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = sort,
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        // Trending alone is reordered in memory; the repository already ordered every other
        // sort correctly and across pages, and re-sorting those would break the cursor.
        var items = sort == GuideSortOrder.Trending
            ? _rankings.Resolve(GuideRankingKeys.Trending).Rank(
                page.Items,
                new GuideRankingContext
                {
                    Filter = filter,
                    At = _clock.UtcNow,
                    ViewerId = _authorization.CallerId
                })
            : page.Items;

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            items.Select(GuideCardMapper.ToCard).ToList(),
            page.NextCursor,
            page.HasMore));
    }
}

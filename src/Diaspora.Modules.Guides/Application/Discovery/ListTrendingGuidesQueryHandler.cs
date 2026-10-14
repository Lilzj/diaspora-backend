using Diaspora.Application.Abstractions;
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
/// Serves the trending shelf.
/// <para>
/// The repository orders the candidates by publication — a stable, indexed base the cursor can
/// page on — and the registered <see cref="GuideRankingKeys.Trending"/> strategy reorders that
/// page in memory. Computing a decayed score in SQL is the one thing the ranking seam exists to
/// keep out of the repository, and a page is at most fifty rows of arithmetic.
/// </para>
/// <para>
/// <b>Worth naming as a limitation:</b> reordering happens within a page, so this improves the
/// order of a sensible page rather than choosing which page. Ordering the whole table by a decay
/// curve needs a materialised score, and that is a later phase's problem, not a reason to push
/// the formula into the database now.
/// </para>
/// </summary>
public sealed class ListTrendingGuidesQueryHandler
    : IQueryHandler<ListTrendingGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideRankingStrategyRegistry _rankings;
    private readonly IClock _clock;
    private readonly GuideAuthorNames _authorNames;

    public ListTrendingGuidesQueryHandler(
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideRankingStrategyRegistry rankings,
        IClock clock,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _rankings = rankings;
        _clock = clock;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListTrendingGuidesQuery query, CancellationToken cancellationToken)
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
            CategoryKey = string.IsNullOrWhiteSpace(query.CategoryKey)
                ? null
                : query.CategoryKey.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Trending,
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        var ranked = _rankings.Resolve(GuideRankingKeys.Trending).Rank(
            page.Items,
            new GuideRankingContext
            {
                Filter = filter,
                At = _clock.UtcNow,
                ViewerId = _authorization.CallerId
            });

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(ranked, cancellationToken),
            page.NextCursor,
            page.HasMore));
    }
}

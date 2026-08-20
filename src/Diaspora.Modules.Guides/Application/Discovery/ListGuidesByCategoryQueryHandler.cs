using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves one category's shelf.
/// <para>
/// A key the catalogue does not know is a "not found", not an empty page: the two look identical
/// to a member, and only one of them tells the client its link is broken.
/// </para>
/// <para>
/// An <em>inactive</em> category still browses. Deactivation stops new guides being filed under
/// it; it does not strand the thousands that already hold the key, and a reader following an old
/// link deserves the guides rather than an error.
/// </para>
/// </summary>
public sealed class ListGuidesByCategoryQueryHandler
    : IQueryHandler<ListGuidesByCategoryQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideCategoryResolver _categories;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideRankingStrategyRegistry _rankings;
    private readonly IClock _clock;

    public ListGuidesByCategoryQueryHandler(
        IGuideRepository guides,
        GuideCategoryResolver categories,
        GuideAuthorizationService authorization,
        GuideRankingStrategyRegistry rankings,
        IClock clock)
    {
        _guides = guides;
        _categories = categories;
        _authorization = authorization;
        _rankings = rankings;
        _clock = clock;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListGuidesByCategoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var category = await _categories.FindAsync(query.CategoryKey, cancellationToken);

        if (category is null)
        {
            return Result.Failure<CursorPagedResult<GuideCardResponse>>(
                GuideErrors.CategoryNotFound);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var sort = GuideWireNames.ParseSort(query.Sort);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CategoryKey = category.Key,
            CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                ? null
                : query.CountryCode.Trim().ToUpperInvariant(),
            Language = string.IsNullOrWhiteSpace(query.Language)
                ? null
                : query.Language.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = sort,
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

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

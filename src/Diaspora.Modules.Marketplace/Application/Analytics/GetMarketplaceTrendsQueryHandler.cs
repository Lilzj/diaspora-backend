using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Analytics;

/// <summary>
/// Reads a trend, grouped by category or by country.
/// <para>
/// The grouping is done by the database, not by loading rollups: "what is moving in this country
/// this month" over a marketplace of any size is a <c>GROUP BY</c>, and a version that materialised
/// every rollup to sum them would work in a test and time out in production.
/// </para>
/// </summary>
public sealed class GetMarketplaceTrendsQueryHandler
    : IQueryHandler<GetMarketplaceTrendsQuery, IReadOnlyList<MarketplaceTrendResponse>>
{
    private const int DefaultWindowDays = 30;

    private readonly IListingAnalyticsRepository _analytics;
    private readonly ListingCategoryResolver _categories;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetMarketplaceTrendsQueryHandler(
        IListingAnalyticsRepository analytics,
        ListingCategoryResolver categories,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _analytics = analytics;
        _categories = categories;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<MarketplaceTrendResponse>>> Handle(
        GetMarketplaceTrendsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<IReadOnlyList<MarketplaceTrendResponse>>(
                MarketplaceErrors.NotModerator);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var from = today.AddDays(-Math.Clamp(query.Days ?? DefaultWindowDays, 1, 365));
        var limit = Math.Clamp(query.Limit ?? 25, 1, 200);

        var byCountry = string.Equals(
            query.GroupBy, "Country", StringComparison.OrdinalIgnoreCase);

        if (byCountry)
        {
            var rows = await _analytics.GetCountryTrendsAsync(
                query.CategoryKey, from, today, limit, cancellationToken);

            return Result.Success<IReadOnlyList<MarketplaceTrendResponse>>(
                [.. rows.Select(row => AnalyticsMapper.ToTrend(row))]);
        }

        var categoryRows = await _analytics.GetCategoryTrendsAsync(
            query.CountryCode, from, today, limit, cancellationToken);

        // The category's display name, resolved in one batched read rather than one per row.
        var names = await _categories.FindManyAsync(
            [.. categoryRows.Select(row => row.Key)], cancellationToken);

        return Result.Success<IReadOnlyList<MarketplaceTrendResponse>>(
        [
            .. categoryRows.Select(row => AnalyticsMapper.ToTrend(
                row, names.GetValueOrDefault(row.Key)?.Name))
        ]);
    }
}

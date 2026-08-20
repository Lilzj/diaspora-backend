using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Assembles the numbers a neighbourhood page renders.
/// <para>
/// <b>The nearby resource counts are the interesting part, and they arrive without Housing knowing
/// what produced them.</b> The brief asks for neighbourhood jobs, neighbourhood marketplace and
/// neighbourhood providers, and the naive way to build that is for Housing to reference Jobs,
/// Marketplace and Providers — which would be the first edges in a graph B2.6 exists to keep
/// acyclic. Instead Housing asks B4's <c>ResourceCountAggregator</c>, which every vertical already
/// contributes a counter to. Jobs does not learn that Housing exists, and a vertical that ships
/// next year appears on the neighbourhood page with no change here at all.
/// </para>
/// <para>
/// <b>Rents are grouped by currency and rent period before anything is averaged.</b> Nothing in
/// this module converts either, so a single figure across them would be wrong in a way nobody
/// reading it could see — and a neighbourhood's headline rent is precisely the number members act
/// on.
/// </para>
/// </summary>
public sealed class NeighborhoodStatisticsService
{
    /// <summary>
    /// How far back the market window reaches. Two years: long enough for a quiet neighbourhood to
    /// accumulate a usable sample, short enough that the figure describes the current market rather
    /// than the one before the last rent rise.
    /// </summary>
    private const int MarketWindowDays = 730;

    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IHousingListingRepository _listings;
    private readonly ResourceCountAggregator _resourceCounts;
    private readonly IClock _clock;

    public NeighborhoodStatisticsService(
        IHousingAnalyticsRepository analytics,
        IHousingListingRepository listings,
        ResourceCountAggregator resourceCounts,
        IClock clock)
    {
        _analytics = analytics;
        _listings = listings;
        _resourceCounts = resourceCounts;
        _clock = clock;
    }

    /// <summary>Everything the neighbourhood page needs in numbers.</summary>
    public async Task<NeighborhoodStatisticsResponse> ForAsync(
        NeighborhoodProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var since = _clock.UtcNow.AddDays(-MarketWindowDays);

        var rows = await _analytics.GetMarketRowsAsync(
            profile.AreaId, countryCode: null, since, cancellationToken);

        var listingCount = await _listings.CountInNeighborhoodAsync(
            profile.AreaId, cancellationToken);

        return new NeighborhoodStatisticsResponse
        {
            ListingCount = listingCount,
            InsightCount = profile.ApprovedInsightCount,
            Market = HousingAnalyticsMapper.ToMarketStatistics(rows),
            MedianDaysToLet = HousingAnalyticsMapper.MedianDaysToLet(rows),
            NearbyResourceCounts = await CountNearbyAsync(profile, cancellationToken)
        };
    }

    /// <summary>
    /// What every other vertical has here, through B4's registry.
    /// <para>
    /// Housing contributes its own counter to the same registry, so the housing figure in this map
    /// comes back through Location rather than being computed twice. Failures degrade to an empty
    /// map: a neighbourhood page without the jobs count is still worth rendering.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>> CountNearbyAsync(
        NeighborhoodProfile profile, CancellationToken cancellationToken)
    {
        if (profile.AreaPath is not { Length: > 0 } path)
        {
            return new Dictionary<string, int>();
        }

        var parsed = HierarchyPath.TryCreate(path);

        if (parsed.IsFailure)
        {
            return new Dictionary<string, int>();
        }

        return await _resourceCounts.CountInAreaAsync(
            AreaId.From(profile.AreaId), parsed.Value, cancellationToken);
    }
}

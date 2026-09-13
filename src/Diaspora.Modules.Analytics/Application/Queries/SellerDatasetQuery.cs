using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own marketplace analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-011, served at <c>GET /api/v1/analytics/sellers/me</c>. The register's
/// aggregation, taken literally: listing-level rollups summed to the seller.
/// </para>
/// <para>
/// <b>An <c>AnalyticsDataset</c>, deliberately, and not a record of its own.</b> Five analytics
/// routes on this platform answer with bespoke records — organisation, event, country,
/// notification, recommendation — and every one of them is served and still unusable by the
/// Phase 15 dashboards, because those render datasets. A seller rollup shaped
/// <c>{ viewCount, totalSales }</c> would have been the sixth.
/// </para>
/// <para>
/// <b><c>/me</c>, and no <c>/sellers/{id}</c> at any authorization level.</b> The creator route's
/// reasoning, which is the strongest guarantee available: a route that cannot name another seller
/// cannot leak one. The register also asked for platform administrators; administrative reads
/// belong on the administration website, which will call a route of its own.
/// </para>
/// <para>
/// <b>What it will not say.</b> Impressions and contacts are not recorded for a listing, so they
/// are not in the set. Revenue is in the set and served as not measured — the wallet is where a
/// seller's money is, and the financial platform is the only authority on it. Nothing here names a
/// buyer: a buyer is a person, and a sales count is analytics where a buyer list is not.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.ContentReadOwn)]
public sealed record GetSellerAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>Whether to include the views series. Off by default, as the creator dataset's is.</summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetSellerAnalyticsDatasetQueryHandler
    : IQueryHandler<GetSellerAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    /// <summary>How many listings the top-listings leaderboard carries.</summary>
    private const int TopListingRows = 10;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly AnalyticsDatasetBuilder _builder;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public GetSellerAnalyticsDatasetQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsReadService reads,
        AnalyticsDatasetBuilder builder,
        IAnalyticsContentDimensionRepository dimensions)
    {
        _context = context;
        _reads = reads;
        _builder = builder;
        _dimensions = dimensions;
    }

    public async Task<Result<AnalyticsDatasetResponse>> Handle(
        GetSellerAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"),
            query.From,
            query.To,
            query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(resolved.Error);
        }

        var period = resolved.Value;
        var baseline = period.Previous();

        var current = await _reads
            .ReadOwnerContentTypeAsync(caller.Value, AnalyticsContentType.Marketplace, period, cancellationToken)
            .ConfigureAwait(false);

        var previous = await _reads
            .ReadOwnerContentTypeAsync(caller.Value, AnalyticsContentType.Marketplace, baseline, cancellationToken)
            .ConfigureAwait(false);

        var metrics = AnalyticsMetricAssembler.BuildSet(
            AnalyticsMetricCatalog.SellerHeadline, current, previous);

        var rows = await _reads
            .ReadOwnerContentTypeDailyAsync(
                caller.Value, AnalyticsContentType.Marketplace, period, cancellationToken)
            .ConfigureAwait(false);

        // Summed per day across the seller's listings. A day with no activity has no rows, and the
        // bucketer walks the calendar asking for each day, so the gap is drawn as a gap.
        var series = query.IncludeSeries
            ? AnalyticsDatasetBuilder.SeriesFrom(
                period,
                rows.GroupBy(row => row.Day)
                    .ToDictionary(group => group.Key, group => (double)group.Sum(row => row.Counts.Views)),
                AnalyticsMetricCatalog.ContentViews,
                _context.Calendar.Today)
            : [];

        var leaderboard = await BuildTopListingsAsync(rows, cancellationToken).ConfigureAwait(false);

        return Result.Success(_builder.Build(
            new AnalyticsSubjectResponse { Type = "seller", Id = null, Label = null },
            period,
            baseline,
            metrics,
            series,
            rows.Count == 0 ? null : rows.Max(row => row.ComputedAt),
            leaderboard));
    }

    /// <summary>
    /// The seller's best-performing listings for the period.
    /// <para>
    /// <b>Ranked by the server, echoed by the client</b>, with sales beside views so a listing that
    /// is seen a great deal and never bought is visible as exactly that. The rows name listings,
    /// never the people who bought them.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<AnalyticsLeaderboardResponse>> BuildTopListingsAsync(
        IReadOnlyList<Domain.Rollups.ContentDailyRollup> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var folded = rows
            .GroupBy(row => row.ContentId)
            .Select(group => new
            {
                ContentId = group.Key,
                Views = group.Sum(row => row.Counts.Views),
                Sales = group.Sum(row => row.Counts.Conversions)
            })
            .Where(entry => entry.Views > 0 || entry.Sales > 0)
            .OrderByDescending(entry => entry.Views)
            .ThenByDescending(entry => entry.Sales)
            .ThenBy(entry => entry.ContentId)
            .Take(TopListingRows)
            .ToList();

        if (folded.Count == 0)
        {
            return [];
        }

        var dimensions = (await _dimensions
                .ListAsync(
                    folded.Select(entry => (AnalyticsContentType.Marketplace, entry.ContentId)).ToList(),
                    cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(dimension => dimension.ContentId);

        var definition = AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ContentViews)!;

        var leaderboardRows = folded
            .Select((entry, index) =>
            {
                dimensions.TryGetValue(entry.ContentId, out var dimension);

                return new AnalyticsLeaderboardRowResponse
                {
                    Position = index + 1,
                    Entity = new AnalyticsEntityRefResponse
                    {
                        EntityType = AnalyticsContentTypes.ToSubjectType(AnalyticsContentType.Marketplace),
                        EntityId = entry.ContentId.ToString(),
                        Title = dimension?.Title ?? "Listing",
                        Route = AnalyticsContentTypes.ToClientRoute(AnalyticsContentType.Marketplace, entry.ContentId)
                    },
                    Value = entry.Views,
                    Format = "count",
                    SecondaryValue = entry.Sales,
                    SecondaryFormat = "count",
                    SecondaryLabel = "Sales"
                };
            })
            .ToList();

        return
        [
            new AnalyticsLeaderboardResponse
            {
                Key = "seller-listings",
                Label = "Top listings",
                MetricKey = definition.CatalogueKey,
                Rows = leaderboardRows,
                NextCursor = null,
                HasMore = false,
                Source = "backend"
            }
        ];
    }
}

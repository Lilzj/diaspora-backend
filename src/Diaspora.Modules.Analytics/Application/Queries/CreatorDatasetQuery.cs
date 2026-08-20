using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own creator analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-009, served at <c>GET /api/v1/analytics/creators/me</c>. The same figures
/// the Phase 7 summary serves, translated rather than recomputed — two renderings of one set of
/// numbers, so the two screens cannot disagree.
/// </para>
/// <para>
/// <b>There is deliberately no <c>GET /analytics/creators/{id}</c>, at any authorization level.</b>
/// The register's own reasoning, and it is the strongest guarantee available: a route that cannot
/// name another creator cannot leak one.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetCreatorAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>
    /// Whether to include the time series.
    /// <para>
    /// Off by default, which is the client's own convention: a summary is cheaper, and §51 asks a
    /// screen to fetch narrowly rather than take everything and render some of it.
    /// </para>
    /// </summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetCreatorAnalyticsDatasetQueryHandler
    : IQueryHandler<GetCreatorAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    /// <summary>How many items the top-content leaderboard carries. The client renders a short list.</summary>
    private const int TopContentRows = 10;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly AnalyticsDatasetBuilder _builder;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public GetCreatorAnalyticsDatasetQueryHandler(
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
        GetCreatorAnalyticsDatasetQuery query, CancellationToken cancellationToken)
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

        var current = await _reads.ReadOwnerAsync(caller.Value, period, cancellationToken)
            .ConfigureAwait(false);

        var previous = await _reads.ReadOwnerAsync(caller.Value, baseline, cancellationToken)
            .ConfigureAwait(false);

        var metrics = AnalyticsMetricAssembler.BuildSet(
            AnalyticsMetricCatalog.CreatorHeadline, current, previous);

        var daily = await _reads.ReadOwnerDailyAsync(caller.Value, period, cancellationToken)
            .ConfigureAwait(false);

        var series = query.IncludeSeries
            ? AnalyticsDatasetBuilder.SeriesFrom(
                period,
                daily.ToDictionary(row => row.Day, row => (double)row.Counts.Views),
                AnalyticsMetricCatalog.ContentViews,
                _context.Calendar.Today)
            : [];

        var leaderboard = await BuildTopContentAsync(caller.Value, period, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(_builder.Build(
            new AnalyticsSubjectResponse { Type = "creator", Id = null, Label = null },
            period,
            baseline,
            metrics,
            series,
            daily.Count == 0 ? null : daily.Max(row => row.ComputedAt),
            leaderboard));
    }

    /// <summary>
    /// The creator's best-performing content for the period.
    /// <para>
    /// <b>Ranked by the server, echoed by the client.</b> The Phase 15 model has no field the client
    /// may sort on and no composite score it may compute — §30's rule applied to a creator's own
    /// list. The rows name items, never people, so the one place the model allows a person to appear
    /// is not used here at all.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<AnalyticsLeaderboardResponse>> BuildTopContentAsync(
        Guid ownerUserId,
        Domain.Periods.AnalyticsPeriod period,
        CancellationToken cancellationToken)
    {
        var rows = await _reads
            .ReadOwnerContentDailyAsync(ownerUserId, period, cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return [];
        }

        var folded = rows
            .GroupBy(row => (row.ContentType, row.ContentId))
            .Select(group => new
            {
                group.Key.ContentType,
                group.Key.ContentId,
                Views = group.Sum(row => row.Counts.Views),
                Engagements = group.Sum(row => row.Counts.Engagements)
            })
            .Where(entry => entry.Views > 0 || entry.Engagements > 0)
            .OrderByDescending(entry => entry.Views)
            .ThenByDescending(entry => entry.Engagements)
            .ThenBy(entry => entry.ContentId)
            .Take(TopContentRows)
            .ToList();

        if (folded.Count == 0)
        {
            return [];
        }

        var dimensions = (await _dimensions
                .ListAsync(
                    folded.Select(entry => (entry.ContentType, entry.ContentId)).ToList(),
                    cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(dimension => (dimension.ContentType, dimension.ContentId));

        var definition = AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ContentViews)!;

        var leaderboardRows = folded
            .Select((entry, index) =>
            {
                dimensions.TryGetValue((entry.ContentType, entry.ContentId), out var dimension);

                return new AnalyticsLeaderboardRowResponse
                {
                    Position = index + 1,
                    Entity = new AnalyticsEntityRefResponse
                    {
                        EntityType = AnalyticsContentTypes.ToSubjectType(entry.ContentType),
                        EntityId = entry.ContentId.ToString(),
                        Title = dimension?.Title ?? "Content",
                        Route = AnalyticsContentTypes.ToClientRoute(entry.ContentType, entry.ContentId)
                    },
                    Value = entry.Views,
                    Format = "count",
                    SecondaryValue = entry.Engagements,
                    SecondaryFormat = "count",
                    SecondaryLabel = "Engagements"
                };
            })
            .ToList();

        return
        [
            new AnalyticsLeaderboardResponse
            {
                Key = "creator-content",
                Label = "Top content",
                MetricKey = definition.CatalogueKey,
                Rows = leaderboardRows,
                NextCursor = null,
                HasMore = false,
                Source = "backend"
            }
        ];
    }
}

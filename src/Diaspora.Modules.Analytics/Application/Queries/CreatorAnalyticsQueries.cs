using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Modules.Analytics.Persistence;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Queries;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-007 — creator analytics and content performance.
//
// Two contracts, and the register's objection to the client doing this is the shape of both:
//
//   "The existing per-item counters answer a different question — a lifetime total, not a figure
//    for a period — so a '30-day views' number derived from them would be wrong in a way nobody
//    could see."
//
// So every figure below comes from a rollup keyed by a day, the period cuts the days, and a metric
// this deployment does not measure is named rather than approximated.
//
// **The subject is never taken from the request.** There is no `creatorId` parameter on either
// route. §18 of the phase brief and §46 of the register agree: a route that cannot name another
// creator cannot leak one, which is stronger than a permission check.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The caller's own creator overview for a period.
/// <para>
/// The Phase 7 <c>CreatorAnalyticsSummary</c> contract, served at
/// <c>GET /api/v1/analytics/creators/me/summary</c>.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.CreatorRead)]
public sealed record GetCreatorAnalyticsSummaryQuery : IQuery<CreatorAnalyticsSummaryResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetCreatorAnalyticsSummaryQueryHandler
    : IQueryHandler<GetCreatorAnalyticsSummaryQuery, CreatorAnalyticsSummaryResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly AnalyticsOptions _options;

    public GetCreatorAnalyticsSummaryQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsReadService reads,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _reads = reads;
        _options = options.Value;
    }

    public async Task<Result<CreatorAnalyticsSummaryResponse>> Handle(
        GetCreatorAnalyticsSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorAnalyticsSummaryResponse>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(query.Period, query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<CreatorAnalyticsSummaryResponse>(resolved.Error);
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

        var series = BuildSeries(period, daily, _context.Calendar.Today);

        return Result.Success(new CreatorAnalyticsSummaryResponse
        {
            Range = ToRange(period),
            ComparisonRange = ToRange(baseline),
            Metrics = metrics,
            Series = series,
            GeneratedAt = _context.Calendar.UtcNow,
            DataAsOf = daily.Count == 0 ? null : daily.Max(row => row.ComputedAt),
            Freshness = "NearRealTime",
            FreshnessNote = FreshnessNote(_options.ProjectionIntervalSeconds),
            DefinitionVersion = AnalyticsMetricCatalog.Version,
            UnavailableMetrics = AnalyticsMetricAssembler.UnavailableIn(metrics)
        });
    }

    /// <summary>
    /// The sparklines, cut into the period's buckets.
    /// <para>
    /// Four series rather than one per metric: views, saves, comments and follower growth. The
    /// client normalises each line separately, so what a chart shows is the shape of each — and
    /// fourteen shapes on one chart is not a chart.
    /// </para>
    /// </summary>
    private static List<CreatorMetricSeriesResponse> BuildSeries(
        AnalyticsPeriod period, IReadOnlyList<OwnerDailyRollup> daily, DateOnly today)
    {
        var series = new List<CreatorMetricSeriesResponse>();

        foreach (var key in AnalyticsMetricCatalog.CreatorSeries)
        {
            var definition = AnalyticsMetricCatalog.Find(key);

            if (definition is null || definition.Availability == AnalyticsMetricAvailability.NotMeasured)
            {
                continue;
            }

            var byDay = daily.ToDictionary(
                row => row.Day,
                row => key switch
                {
                    AnalyticsMetricCatalog.ContentViews => (double)row.Counts.Views,
                    AnalyticsMetricCatalog.Saves => row.Counts.Saves,
                    AnalyticsMetricCatalog.Comments => row.Counts.Comments,
                    AnalyticsMetricCatalog.FollowerGrowth =>
                        row.Counts.FollowsGained - row.Counts.FollowsLost,
                    _ => 0d
                });

            series.Add(new CreatorMetricSeriesResponse
            {
                Key = definition.Key,
                Label = definition.Label,
                Granularity = AnalyticsMetricAssembler.GranularityName(period.Granularity),
                Points = AnalyticsMetricAssembler.Bucket(period, byDay, today)
            });
        }

        return series;
    }

    internal static AnalyticsRangeResponse ToRange(AnalyticsPeriod period) => new()
    {
        Period = AnalyticsQueryContext.ToClientPeriod(period.Preset),
        From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Timezone = period.TimeZoneId
    };

    internal static string FreshnessNote(int intervalSeconds) => string.Create(
        CultureInfo.InvariantCulture,
        $"Figures are recomputed about every {Math.Max(5, intervalSeconds)} seconds from the activity the platform recorded, so today is always still filling in.");
}

/// <summary>
/// The caller's own content performance, cursor-paged.
/// <para>
/// The Phase 7 <c>CreatorContentPerformancePage</c> contract, served at
/// <c>GET /api/v1/analytics/creators/me/content</c>. §39 forbids loading a whole dataset, and the
/// register asks for a cursor "from the first request" rather than an offset that skips rows when
/// something new lands between pages.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.ContentReadOwn)]
public sealed record GetCreatorContentPerformanceQuery : IQuery<CreatorContentPerformancePageResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>The client's own vocabulary — <c>Guide</c>, <c>Update</c>, … — or <c>all</c>.</summary>
    public string? ContentType { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class GetCreatorContentPerformanceQueryHandler
    : IQueryHandler<GetCreatorContentPerformanceQuery, CreatorContentPerformancePageResponse>
{
    /// <summary>The client's page size. Fifteen, as the Phase 7 screen requests.</summary>
    private const int DefaultTake = 15;

    private const int MaximumTake = 50;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public GetCreatorContentPerformanceQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsReadService reads,
        IAnalyticsContentDimensionRepository dimensions)
    {
        _context = context;
        _reads = reads;
        _dimensions = dimensions;
    }

    public async Task<Result<CreatorContentPerformancePageResponse>> Handle(
        GetCreatorContentPerformanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorContentPerformancePageResponse>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(query.Period, query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<CreatorContentPerformancePageResponse>(resolved.Error);
        }

        var period = resolved.Value;
        var baseline = period.Previous();
        var take = Math.Clamp(query.Take ?? DefaultTake, 1, MaximumTake);

        var wanted = ParseContentTypeFilter(query.ContentType);

        // Everything the owner has published is the universe, not everything with activity: an item
        // with no views in the period is a row reading zero, which is a fact a creator wants. A list
        // built from the rollups alone would silently omit exactly the content that is not working.
        var dimensions = (await _dimensions
                .ListForOwnerAsync(caller.Value, cancellationToken)
                .ConfigureAwait(false))
            .Where(row => wanted is null || row.ContentType == wanted)
            .ToList();

        if (dimensions.Count == 0)
        {
            return Result.Success(new CreatorContentPerformancePageResponse
            {
                Items = [],
                NextCursor = null,
                HasMore = false
            });
        }

        var currentRows = await _reads
            .ReadOwnerContentDailyAsync(caller.Value, period, cancellationToken)
            .ConfigureAwait(false);

        var baselineRows = await _reads
            .ReadOwnerContentDailyAsync(caller.Value, baseline, cancellationToken)
            .ConfigureAwait(false);

        var current = Fold(currentRows);
        var previous = Fold(baselineRows);

        var ordered = dimensions
            .Select(dimension =>
            {
                var key = (dimension.ContentType, dimension.ContentId);

                current.TryGetValue(key, out var now);
                previous.TryGetValue(key, out var then);

                return new
                {
                    Dimension = dimension,
                    Current = now ?? AnalyticsCounts.Zero,
                    Baseline = then,
                    Views = now?.Views ?? 0
                };
            })

            // Most-viewed first, and the tie-break is the content identifier rather than a title:
            // an ordering that is not total cannot be paged, because two pages fetched a second
            // apart would disagree about which row came first and one item would be served twice.
            .OrderByDescending(entry => entry.Views)
            .ThenByDescending(entry => entry.Dimension.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Dimension.ContentId)
            .ToList();

        var start = ResumeIndex(
            ordered.Select(entry => (entry.Views, entry.Dimension.PublishedAt, entry.Dimension.ContentId))
                .ToList(),
            AnalyticsCursor.Parse(query.Cursor));

        var page = ordered.Skip(start).Take(take).ToList();
        var hasMore = start + page.Count < ordered.Count;

        // An item with no rows in the comparison period may have had a quiet one rather than no
        // history — the tiles' rule, applied per row, so a figure does not mean one thing on the
        // overview and another on its own row. Read once, and only when a row needs it.
        IReadOnlySet<(AnalyticsContentType ContentType, Guid ContentId)> recordedBeforeBaseline =
            page.Exists(entry => entry.Baseline is null)
                ? await _reads
                    .ReadOwnerContentRecordedBeforeAsync(caller.Value, baseline.FromDate, cancellationToken)
                    .ConfigureAwait(false)
                : new HashSet<(AnalyticsContentType ContentType, Guid ContentId)>();

        var items = page
            .Select(entry => new CreatorContentPerformanceResponse
            {
                ContentId = entry.Dimension.ContentId.ToString(),
                ContentType = AnalyticsContentTypes.ToClientContentType(entry.Dimension.ContentType),
                Title = entry.Dimension.Title ?? FallbackTitle(entry.Dimension.ContentType),
                PublishedAt = entry.Dimension.PublishedAt,
                Status = entry.Dimension.Status ?? "Published",
                // Each row is one item: only the columns its kind records, and its conversions carry
                // the item's own name — "Sales" on a listing's row, "Applications" on a job's — in a
                // list that mixes both.
                Metrics = AnalyticsMetricAssembler.NameConversions(
                    AnalyticsMetricAssembler.BuildSet(
                        AnalyticsMetricCatalog.ContentItemFor(entry.Dimension.ContentType),
                        new AnalyticsWindowTotals(entry.Current, 0, 0, HasData: true, HasHistory: true),
                        new AnalyticsWindowTotals(
                            entry.Baseline ?? AnalyticsCounts.Zero,
                            0,
                            0,
                            HasData: entry.Baseline is not null,
                            HasHistory: recordedBeforeBaseline.Contains(
                                (entry.Dimension.ContentType, entry.Dimension.ContentId)))),
                    entry.Dimension.ContentType),

                // Always false, and stated on the contract rather than guessed. Whether a placement
                // is active is B15's and is not carried on anything this module consumes.
                IsSponsored = false
            })
            .ToList();

        var last = page.Count == 0 ? null : page[^1];

        return Result.Success(new CreatorContentPerformancePageResponse
        {
            Items = items,
            NextCursor = hasMore && last is not null
                ? AnalyticsCursor
                    .For(last.Views, last.Dimension.PublishedAt, last.Dimension.ContentId)
                    .Encode()
                : null,
            HasMore = hasMore
        });
    }

    /// <summary>
    /// Where the next page starts.
    /// <para>
    /// <b>By identity first, by the sort key second, and the fallback is the interesting half.</b>
    /// The ordinary case finds the boundary row and resumes after it, which is stable however much
    /// the ranking moved between requests. When the boundary row is gone — the item was deleted, or
    /// its type filter changed — its identity is no longer in the list, and resuming from the first
    /// row that sorts strictly after the boundary's key is the closest correct answer. Falling back
    /// to the first page instead would silently restart an infinite scroll.
    /// </para>
    /// </summary>
    private static int ResumeIndex(
        List<(long Views, DateTimeOffset? PublishedAt, Guid ContentId)> ordered,
        AnalyticsCursor? cursor)
    {
        if (cursor is null)
        {
            return 0;
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].ContentId == cursor.ContentId)
            {
                return index + 1;
            }
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var entry = ordered[index];
            var ticks = (entry.PublishedAt ?? DateTimeOffset.MinValue).UtcTicks;

            if (entry.Views < cursor.Views
                || (entry.Views == cursor.Views && ticks < cursor.PublishedTicks))
            {
                return index;
            }
        }

        return ordered.Count;
    }

    /// <summary>
    /// Sums a window's daily rows per item.
    /// <para>
    /// In memory rather than in SQL, and the bound is what makes that acceptable: the rows are one
    /// owner's, over one period, already filtered by an index on <c>(owner, day)</c>. A creator with
    /// two hundred items over ninety days is eighteen thousand rows at the very most, and in
    /// practice far fewer because a day with no activity has no row.
    /// </para>
    /// </summary>
    private static Dictionary<(AnalyticsContentType, Guid), AnalyticsCounts> Fold(
        IReadOnlyList<ContentDailyRollup> rows)
    {
        var folded = new Dictionary<(AnalyticsContentType, Guid), AnalyticsCounts>();

        foreach (var row in rows)
        {
            var key = (row.ContentType, row.ContentId);

            folded[key] = folded.TryGetValue(key, out var existing)
                ? existing.Add(row.Counts)
                : row.Counts;
        }

        return folded;
    }

    private static AnalyticsContentType? ParseContentTypeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : AnalyticsContentTypes.FromRouteSegment(value) is var parsed
                && parsed != AnalyticsContentType.Unknown
                ? parsed
                : AnalyticsContentTypes.FromCommunity(value) is var fromCommunity
                    && fromCommunity != AnalyticsContentType.Unknown
                    ? fromCommunity
                    : AnalyticsContentType.Unknown;

    private static string FallbackTitle(AnalyticsContentType contentType) => contentType switch
    {
        AnalyticsContentType.Post => "Community update",
        AnalyticsContentType.Guide => "Guide",
        AnalyticsContentType.Event => "Event",
        AnalyticsContentType.Job => "Job",
        AnalyticsContentType.Housing => "Housing listing",
        AnalyticsContentType.Marketplace => "Marketplace listing",
        AnalyticsContentType.Provider => "Provider profile",
        AnalyticsContentType.Organization => "Organization",
        _ => "Content"
    };
}

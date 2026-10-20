using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Content.V1;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The platform overview: what exists across Diaspora now, and what was added day by day.
/// <para>
/// FRONTEND-PHASE15-GAP-001, served at <c>GET /api/v1/analytics/platform</c> in the shared Phase 15
/// dataset shape the platform screen already reads. Two kinds of figure, and the difference is the
/// point of the route:
/// </para>
/// <list type="bullet">
/// <item><b>Totals</b> (<c>inventory.*</c>, <c>audience.users.total</c>) — live items now, counted
/// by the owning module from its own table. A total at a past date is not recorded anywhere, so a
/// total carries no comparison rather than a guessed one.</item>
/// <item><b>Additions</b> (<c>growth.*.new</c>, <c>audience.users.new</c>) — items first published
/// (members: registered) inside the period, compared with the period before, and served as a
/// series. A republished edit is not new: the sources count first publication only.</item>
/// </list>
/// <para>
/// <b>Open to every signed-in member.</b> Every figure is an aggregate count over public inventory or
/// a membership total; no row names an item or a person. The product decision (20 Oct 2026) is
/// that these counts are not administrative, so the route takes the member-scoped analytics read
/// rather than <c>analytics:platform:read</c>.
/// </para>
/// <para>
/// <b>Never invented.</b> A source that is not registered on this deployment is reported in
/// <c>unavailableMetrics</c> and the dataset is <c>PARTIAL</c>; nothing is filled in for it.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetPlatformAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    /// <summary><c>7d</c>, <c>30d</c> (the default), <c>90d</c>, <c>today</c>, <c>year</c> or <c>custom</c>.</summary>
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetPlatformAnalyticsDatasetQueryHandler
    : IQueryHandler<GetPlatformAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    /// <summary>
    /// The rows of the overview, in the order they are served. The keys are the client's catalogue
    /// keys (<c>lib/analytics/definitions.ts</c>), so no translation sits between the two.
    /// </summary>
    internal static readonly IReadOnlyList<PlatformRow> Rows =
    [
        new("guides", "inventory.guides", "Guides", "growth.guides.new", "New guides", IsContent: true),
        new("jobs", "inventory.jobs", "Jobs", "growth.jobs.new", "New jobs", IsContent: true),
        new("listings", "inventory.listings", "Marketplace listings", "growth.listings.new", "New marketplace listings", IsContent: true),
        new("housing", "inventory.housing", "Housing listings", "growth.housing.new", "New housing listings", IsContent: true),
        new("providers", "inventory.providers", "Service providers", "growth.providers.new", "New service providers", IsContent: true),
        new("members", "audience.users.total", "Members", "audience.users.new", "New members", IsContent: false)
    ];

    internal const string ContentNewKey = "growth.content.new";

    private readonly AnalyticsQueryContext _context;
    private readonly IEnumerable<IPlatformInventorySource> _sources;

    public GetPlatformAnalyticsDatasetQueryHandler(
        AnalyticsQueryContext context, IEnumerable<IPlatformInventorySource> sources)
    {
        _context = context;
        _sources = sources;
    }

    public async Task<Result<AnalyticsDatasetResponse>> Handle(
        GetPlatformAnalyticsDatasetQuery query, CancellationToken cancellationToken)
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
        var zone = AnalyticsReportingCalendar.TryResolve(period.TimeZoneId) ?? _context.Calendar.Zone;
        var now = _context.Calendar.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        var byKey = _sources
            .GroupBy(source => source.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var freshness = new DataFreshnessResponse
        {
            ComputedAt = now,
            SourceName = "Diaspora platform inventory",
            Methodology = string.Create(
                CultureInfo.InvariantCulture,
                $"Counted at request time from each module's own records. Totals are live items now; additions are items first published (members: registered) in the period and still on the platform. Days are cut in {period.TimeZoneId}."),
            Limitations =
            [
                "A total at a past date is not recorded, so totals carry no comparison.",
                "Deleted items and closed accounts are not counted, including in past days."
            ]
        };

        var metrics = new List<AnalyticsMetricResponse>();
        var series = new List<AnalyticsSeriesResponse>();
        var unavailable = new List<string>();
        var contentDaily = new Dictionary<DateOnly, double>();
        long contentCurrent = 0;
        long contentPrevious = 0;
        var contentServed = false;

        foreach (var row in Rows)
        {
            if (!byKey.TryGetValue(row.SourceKey, out var source))
            {
                unavailable.Add(row.TotalKey);
                unavailable.Add(row.NewKey);
                continue;
            }

            // One read over both windows: the baseline ends where the period starts.
            var counted = await source
                .CountAsync(baseline.Start, period.End, cancellationToken)
                .ConfigureAwait(false);

            var inPeriod = counted.AddedAt.Where(period.Contains).ToList();
            var inBaseline = counted.AddedAt.Count(baseline.Contains);

            var daily = inPeriod
                .GroupBy(instant => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime))
                .ToDictionary(group => group.Key, group => (double)group.Count());

            metrics.Add(Total(row, counted.CurrentTotal, freshness));
            metrics.Add(Added(row.NewKey, row.NewLabel, inPeriod.Count, inBaseline, baseline, freshness));
            series.Add(Series(row.NewKey, row.NewLabel, period, daily, today));

            if (row.IsContent)
            {
                contentServed = true;
                contentCurrent += inPeriod.Count;
                contentPrevious += inBaseline;

                foreach (var (day, value) in daily)
                {
                    contentDaily[day] = contentDaily.GetValueOrDefault(day) + value;
                }
            }
        }

        if (contentServed)
        {
            metrics.Add(Added(
                ContentNewKey, "New content", contentCurrent, contentPrevious, baseline, freshness));
            series.Add(Series(ContentNewKey, "New content", period, contentDaily, today));
        }
        else
        {
            unavailable.Add(ContentNewKey);
        }

        return Result.Success(new AnalyticsDatasetResponse
        {
            Subject = new AnalyticsSubjectResponse { Type = "platform", Id = null, Label = "Diaspora" },
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = AnalyticsQueryContext.ToDatasetPreset(period.Preset),
                From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Timezone = period.TimeZoneId,
                IsDeviceTimezone = false
            },
            Comparison = "previousPeriod",
            Status = unavailable.Count == 0 ? "LIVE" : "PARTIAL",
            Metrics = metrics,
            Series = series,
            Breakdowns = [],
            Funnels = [],
            Leaderboards = [],
            Insights = [],
            Freshness = freshness,
            UnavailableMetrics = unavailable
        });
    }

    private static AnalyticsMetricResponse Total(
        PlatformRow row, long value, DataFreshnessResponse freshness) => new()
    {
        Key = row.TotalKey,
        Label = row.TotalLabel,
        Value = new SourcedValueResponse { Value = value, Source = "backend" },
        Format = "count",
        Computation = "server-aggregate",
        HigherIsBetter = true,
        Comparison = new AnalyticsComparisonResponse
        {
            Basis = "previousPeriod",
            UnavailableReason = "A total at a past date is not recorded."
        },
        Freshness = freshness,
        PrivacyClass = "public",
        IsSuppressed = false
    };

    private static AnalyticsMetricResponse Added(
        string key,
        string label,
        long current,
        long previous,
        AnalyticsPeriod baseline,
        DataFreshnessResponse freshness) => new()
    {
        Key = key,
        Label = label,
        Value = new SourcedValueResponse { Value = current, Source = "backend" },
        Format = "count",
        Computation = "server-aggregate",
        HigherIsBetter = true,
        Comparison = new AnalyticsComparisonResponse
        {
            Basis = "previousPeriod",
            BaselineFrom = baseline.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BaselineTo = baseline.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BaselineValue = previous,

            // A ratio, as every dataset comparison is. Undefined from zero, and said so.
            ChangePercent = previous == 0 ? null : (current - previous) / (double)previous,
            ChangeAbsolute = current - previous,
            UnavailableReason = previous == 0 ? "Nothing was added in the previous period." : null
        },
        Freshness = freshness,
        PrivacyClass = "public",
        IsSuppressed = false
    };

    private static AnalyticsSeriesResponse Series(
        string key,
        string label,
        AnalyticsPeriod period,
        IReadOnlyDictionary<DateOnly, double> daily,
        DateOnly today) => new()
    {
        Key = key,
        Label = label,
        Granularity = AnalyticsMetricAssembler.GranularityName(period.Granularity),
        Format = "count",
        Points = AnalyticsMetricAssembler.Bucket(period, daily, today)
            .Select(point => new AnalyticsPointResponse
            {
                At = point.At,
                Value = point.Value,
                IsPartialBucket = point.IsPartialBucket
            })
            .ToList(),
        Source = "backend"
    };

    internal sealed record PlatformRow(
        string SourceKey,
        string TotalKey,
        string TotalLabel,
        string NewKey,
        string NewLabel,
        bool IsContent);
}

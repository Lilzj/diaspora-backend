using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Entitlements;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The advanced summary the <c>analytics.advanced</c> entitlement unlocks — MON-GAP-010, in the
/// client's <c>PremiumAnalytics</c> shape.
/// <para>
/// <b>The gate is on the server.</b> Without the entitlement the answer is 403 and no figure is
/// computed, so premium data never reaches a device that has not bought it. The entitlement is read
/// from Financial's active-subscription rule through <see cref="IMemberEntitlementReader"/>.
/// </para>
/// <para>
/// <b>The caller's own work, counted.</b> Views, engagements, saves, enquiries and profile visits
/// from the daily rollups, a daily series, views by kind of content, the most-viewed items, and views
/// by the surface the app reported them on. Where the audience was and who it was are not recorded,
/// and are listed in <c>unavailable</c> rather than drawn.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetPremiumAnalyticsSummaryQuery : IQuery<PremiumAnalyticsSummaryResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetPremiumAnalyticsSummaryQueryHandler
    : IQueryHandler<GetPremiumAnalyticsSummaryQuery, PremiumAnalyticsSummaryResponse>
{
    /// <summary>The entitlement key the summary is sold under.</summary>
    public const string AdvancedEntitlement = "analytics.advanced";

    private const int TopSubjectRows = 5;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly IAnalyticsEventRepository _events;
    private readonly IAnalyticsContentDimensionRepository _dimensions;
    private readonly IEnumerable<IMemberEntitlementReader> _entitlements;
    private readonly AnalyticsOptions _options;

    public GetPremiumAnalyticsSummaryQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsReadService reads,
        IAnalyticsEventRepository events,
        IAnalyticsContentDimensionRepository dimensions,
        IEnumerable<IMemberEntitlementReader> entitlements,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _reads = reads;
        _events = events;
        _dimensions = dimensions;
        _entitlements = entitlements;
        _options = options.Value;
    }

    public async Task<Result<PremiumAnalyticsSummaryResponse>> Handle(
        GetPremiumAnalyticsSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<PremiumAnalyticsSummaryResponse>(caller.Error);
        }

        // No reader registered grants nothing: the safe direction.
        var granted = _entitlements.FirstOrDefault() is { } reader
            ? await reader.GrantedKeysAsync(caller.Value, cancellationToken).ConfigureAwait(false)
            : new HashSet<string>(StringComparer.Ordinal);

        if (!granted.Contains(AdvancedEntitlement))
        {
            return Result.Failure<PremiumAnalyticsSummaryResponse>(AnalyticsErrors.PremiumNotEntitled);
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"), query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<PremiumAnalyticsSummaryResponse>(resolved.Error);
        }

        var period = resolved.Value;
        var baseline = period.Previous();
        var owner = caller.Value;

        var current = await _reads.ReadOwnerAsync(owner, period, cancellationToken).ConfigureAwait(false);
        var previous = await _reads.ReadOwnerAsync(owner, baseline, cancellationToken).ConfigureAwait(false);

        var headline = new List<PremiumAnalyticsMetricResponse>
        {
            Metric("views", "Views", current.Counts.Views, previous.Counts.Views,
                Definition(AnalyticsMetricCatalog.ContentViews)),
            Metric("engagements", "Engagements", current.Counts.Engagements, previous.Counts.Engagements,
                "Saves, shares, comments and reactions on what you published."),
            Metric("saves", "Saves", current.Counts.Saves, previous.Counts.Saves,
                Definition(AnalyticsMetricCatalog.Saves)),
            Metric("enquiries", "Enquiries", current.Counts.Enquiries, previous.Counts.Enquiries,
                Definition(AnalyticsMetricCatalog.Enquiries)),
            Metric("profileVisits", "Profile visits", current.ProfileViews, previous.ProfileViews,
                Definition(AnalyticsMetricCatalog.ProfileViews))
        };

        var contentRows = await _reads.ReadOwnerContentDailyAsync(owner, period, cancellationToken).ConfigureAwait(false);
        var items = AnalyticsItemReads.Fold(contentRows);

        var breakdowns = new List<PremiumAnalyticsBreakdownResponse>();
        var unavailable = new List<AnalyticsUnavailablePartResponse>
        {
            new()
            {
                Key = "country",
                Label = "Where your audience is",
                Reason = "The country of the people who open your work is not recorded."
            },
            new()
            {
                Key = "audienceType",
                Label = "Who they are",
                Reason = "Nothing records whether somebody who opened your work is a new arrival, settled or a student."
            }
        };

        var totalViews = items.Sum(item => item.Views);

        if (totalViews > 0)
        {
            breakdowns.Add(new PremiumAnalyticsBreakdownResponse
            {
                Dimension = "contentType",
                Label = "Views by kind of content",
                Buckets =
                [
                    .. items
                        .GroupBy(item => item.ContentType)
                        .Select(group => (Type: group.Key, Views: group.Sum(item => item.Views)))
                        .Where(entry => entry.Views > 0)
                        .OrderByDescending(entry => entry.Views)
                        .ThenBy(entry => entry.Type)
                        .Select(entry => new PremiumAnalyticsBucketResponse
                        {
                            Label = AnalyticsContentTypes.ToClientContentType(entry.Type),
                            Value = entry.Views,
                            Ratio = entry.Views / (double)totalViews
                        })
                ]
            });
        }

        var now = _context.Calendar.UtcNow;

        // Surfaces are on the raw rows only, which are kept for a bounded window.
        if (period.Start < now.AddDays(-_options.RawEventRetentionDays))
        {
            unavailable.Add(new AnalyticsUnavailablePartResponse
            {
                Key = "source",
                Label = "Where they opened it",
                Reason = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Where a view happened is kept for {_options.RawEventRetentionDays} days, and this period starts before that.")
            });
        }
        else
        {
            var surfaces = await _events
                .CountOwnerViewsBySurfaceAsync(owner, period.Start, period.End, cancellationToken)
                .ConfigureAwait(false);

            var recorded = surfaces.Sum(entry => entry.Views);

            if (recorded > 0)
            {
                breakdowns.Add(new PremiumAnalyticsBreakdownResponse
                {
                    Dimension = "source",
                    Label = "Where they opened it",
                    Buckets =
                    [
                        .. surfaces
                            .OrderByDescending(entry => entry.Views)
                            .ThenBy(entry => entry.Surface ?? "~", StringComparer.Ordinal)
                            .Select(entry => new PremiumAnalyticsBucketResponse
                            {
                                Label = string.IsNullOrWhiteSpace(entry.Surface) ? "Not reported" : entry.Surface,
                                Value = entry.Views,
                                Ratio = entry.Views / (double)recorded
                            })
                    ]
                });
            }
        }

        var ownerDays = (await _reads.ReadOwnerDailyAsync(owner, period, cancellationToken).ConfigureAwait(false))
            .GroupBy(row => row.Day)
            .ToDictionary(
                group => group.Key,
                group => (Views: group.Sum(row => row.Counts.Views), Engagements: group.Sum(row => row.Counts.Engagements)));

        var lastDay = period.ToDate > _context.Calendar.Today ? _context.Calendar.Today : period.ToDate;
        var series = new List<PremiumAnalyticsPointResponse>();

        for (var day = period.FromDate; day <= lastDay; day = day.AddDays(1))
        {
            var found = ownerDays.TryGetValue(day, out var figures);

            series.Add(new PremiumAnalyticsPointResponse
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Views = found ? figures.Views : 0,
                Engagements = found ? figures.Engagements : 0
            });
        }

        var top = items
            .Where(item => item.Views > 0 || item.Engagements > 0)
            .OrderByDescending(item => item.Views)
            .ThenByDescending(item => item.Engagements)
            .ThenBy(item => item.ContentId)
            .Take(TopSubjectRows)
            .ToList();

        var titles = top.Count == 0
            ? new Dictionary<(Domain.Enums.AnalyticsContentType, Guid), string?>()
            : (await _dimensions
                    .ListAsync(top.Select(item => (item.ContentType, item.ContentId)).ToList(), cancellationToken)
                    .ConfigureAwait(false))
                .ToDictionary(dimension => (dimension.ContentType, dimension.ContentId), dimension => dimension.Title);

        return Result.Success(new PremiumAnalyticsSummaryResponse
        {
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            Timezone = period.TimeZoneId,
            Headline = headline,
            Breakdowns = breakdowns,
            Series = series,
            TopSubjects =
            [
                .. top.Select(item => new PremiumAnalyticsSubjectResponse
                {
                    SubjectType = AnalyticsContentTypes.ToClientContentType(item.ContentType),
                    SubjectId = item.ContentId.ToString(),
                    Title = titles.TryGetValue((item.ContentType, item.ContentId), out var title) && title is not null
                        ? title
                        : "Content",
                    Views = item.Views,
                    Engagements = item.Engagements
                })
            ],
            ReportingLagHours = _options.ProjectionIntervalSeconds / 3600d,
            Unavailable = unavailable,
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = "Diaspora analytics",
                Methodology = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Summed from the daily figures for everything you published, against the {baseline.ToDate.DayNumber - baseline.FromDate.DayNumber + 1} days before. Days are cut in {period.TimeZoneId}."),
                Limitations =
                [
                    "A change is null where the earlier period had nothing to compare against.",
                    "Where a view happened is what the app reported; a view it reported without one is \"Not reported\"."
                ]
            }
        });
    }

    private static PremiumAnalyticsMetricResponse Metric(
        string key, string label, long current, long previous, string definition) => new()
    {
        Key = key,
        Label = label,
        Value = current,
        Unit = "count",
        ChangeRatio = previous == 0 ? null : (current - previous) / (double)previous,
        Definition = definition
    };

    private static string Definition(string catalogueKey) =>
        AnalyticsMetricCatalog.Find(catalogueKey)?.Definition ?? string.Empty;
}

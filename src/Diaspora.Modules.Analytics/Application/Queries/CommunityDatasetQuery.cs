using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityProgramme.V1.Analytics;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// One community's membership over a period, for the people who run it.
/// <para>
/// FRONTEND-PHASE15-GAP-003, served at <c>GET /api/v1/analytics/communities/{id}</c> in the shared
/// Phase 15 dataset shape. The figures are counted by the module that owns communities, through
/// <see cref="ICommunityAnalyticsSource"/>, which also decides who may read them: the community's
/// moderators, administrators and owner, and staff. Anybody else — including an ordinary member —
/// receives the same 404 as for a community that does not exist.
/// </para>
/// <para>
/// <b>Membership only.</b> Nothing this platform records about activity — posts, events, notices —
/// is keyed by a community, so activity figures are named in <c>unavailableMetrics</c> rather than
/// approximated from the community's country of origin.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.CreatorRead)]
public sealed record GetCommunityAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    /// <summary>The community's identifier or slug.</summary>
    public required string CommunityId { get; init; }

    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetCommunityAnalyticsDatasetQueryHandler
    : IQueryHandler<GetCommunityAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    internal const string MembersKey = "reach.members";
    internal const string JoinedKey = "audience.users.new";
    internal const string NetKey = "growth.members.net";

    /// <summary>
    /// Figures the client's community dashboard can ask for and this platform cannot count, with the
    /// reason travelling in the dataset's limitations.
    /// </summary>
    internal static readonly IReadOnlyList<string> Unavailable =
    [
        "growth.members.rate",
        "audience.users.active.weekly",
        "audience.users.active.monthly",
        "content.published",
        "event.rsvps"
    ];

    private readonly AnalyticsQueryContext _context;
    private readonly IEnumerable<ICommunityAnalyticsSource> _sources;

    public GetCommunityAnalyticsDatasetQueryHandler(
        AnalyticsQueryContext context, IEnumerable<ICommunityAnalyticsSource> sources)
    {
        _context = context;
        _sources = sources;
    }

    public async Task<Result<AnalyticsDatasetResponse>> Handle(
        GetCommunityAnalyticsDatasetQuery query, CancellationToken cancellationToken)
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

        if (_sources.FirstOrDefault() is not { } source)
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound);
        }

        // One read over both windows: the baseline ends where the period starts.
        var reading = await source
            .ReadAsync(
                query.CommunityId,
                caller.Value,
                _context.IsPlatformReader,
                baseline.Start,
                period.End,
                cancellationToken)
            .ConfigureAwait(false);

        // Not there, and not yours to read, answer alike: a status code must not confirm that a
        // private community exists.
        if (reading is null || !reading.ViewerMayRead)
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound);
        }

        var zone = AnalyticsReportingCalendar.TryResolve(period.TimeZoneId) ?? _context.Calendar.Zone;
        var now = _context.Calendar.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        var joined = reading.JoinedAt.Where(period.Contains).ToList();
        var joinedBefore = reading.JoinedAt.Count(baseline.Contains);
        var ended = reading.EndedAt.Count(period.Contains);
        var endedBefore = reading.EndedAt.Count(baseline.Contains);

        var freshness = new DataFreshnessResponse
        {
            ComputedAt = now,
            SourceName = "Diaspora community memberships",
            Methodology = string.Create(
                CultureInfo.InvariantCulture,
                $"Counted at request time from the community's membership records. Members are joined members, verified or not; following and interest are separate relationships and are not counted. Days are cut in {period.TimeZoneId}."),
            Limitations =
            [
                "A member count at a past date is not recorded, so the member total carries no comparison.",
                "A member who left and rejoined is counted at their latest join only.",
                "Activity in a community — posts, events and notices — is not recorded per community on this deployment."
            ]
        };

        var daily = joined
            .GroupBy(instant => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime))
            .ToDictionary(group => group.Key, group => (double)group.Count());

        var metrics = new List<AnalyticsMetricResponse>
        {
            new()
            {
                Key = MembersKey,
                Label = "Members",
                Value = new SourcedValueResponse { Value = reading.CurrentMembers, Source = "backend" },
                Format = "count",
                Computation = "server-aggregate",
                HigherIsBetter = true,
                Comparison = new AnalyticsComparisonResponse
                {
                    Basis = "previousPeriod",
                    UnavailableReason = "A member count at a past date is not recorded."
                },
                Freshness = freshness,
                PrivacyClass = "privileged"
            },
            Changed(JoinedKey, "New members", joined.Count, joinedBefore, baseline, freshness),
            Changed(
                NetKey,
                "Net member change",
                joined.Count - ended,
                joinedBefore - endedBefore,
                baseline,
                freshness)
        };

        var series = new List<AnalyticsSeriesResponse>
        {
            new()
            {
                Key = JoinedKey,
                Label = "New members",
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
            }
        };

        return Result.Success(new AnalyticsDatasetResponse
        {
            Subject = new AnalyticsSubjectResponse
            {
                Type = "community",
                Id = reading.CommunityId.ToString(),
                Label = reading.Name
            },
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = AnalyticsQueryContext.ToDatasetPreset(period.Preset),
                From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Timezone = period.TimeZoneId,
                IsDeviceTimezone = false
            },
            Comparison = "previousPeriod",
            Status = "PARTIAL",
            Metrics = metrics,
            Series = series,
            Breakdowns = [],
            Funnels = [],
            Leaderboards = [],
            Insights = [],
            Freshness = freshness,
            UnavailableMetrics = Unavailable
        });
    }

    private static AnalyticsMetricResponse Changed(
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
            ChangePercent = previous == 0 ? null : (current - previous) / (double)Math.Abs(previous),
            ChangeAbsolute = current - previous,
            UnavailableReason = previous == 0 ? "zeroBaseline" : null
        },
        Freshness = freshness,
        PrivacyClass = "privileged"
    };
}

using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Insights;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-014 — organization dashboard analytics.
// COMMUNITY-LIFE-GAP-017 — community impact metrics.
//
// Two register requirements shape both, and each is answered rather than deferred:
//
//   GAP-014 asks for "a documented recomputation cadence, so the UI can say how fresh a figure is
//   rather than implying it is live". These reads are computed FROM THE ROWS on every request, so the
//   cadence is "on read" and computedAt is the moment the request was served. That is a deliberate
//   trade against a nightly projection: a projection is a second place for a number to be wrong, and
//   the queries here are bounded by an organization and a window.
//
//   GAP-017 asks for "a documented definition for every metric — an undefined metric is worse than an
//   absent one". Every figure travels with its definition and its source module, so the sentence
//   explaining what was counted is on the same screen as the number.
//
// What is NOT here: money. Funds raised is B15's figure and B15.1 owns the campaign that would total
// it; this module reports what it owns and does not invent a financial number.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An organization's programme analytics, over a period.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ViewAnalytics)]
public sealed record GetOrganizationAnalyticsQuery : IQuery<OrganizationAnalyticsResponse>
{
    public required Guid OrganizationId { get; init; }

    /// <summary><c>7d</c>, <c>30d</c>, <c>90d</c>, <c>12m</c> or <c>all</c>.</summary>
    public string? Period { get; init; }
}

public sealed class GetOrganizationAnalyticsQueryHandler
    : IQueryHandler<GetOrganizationAnalyticsQuery, OrganizationAnalyticsResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IClock _clock;

    public GetOrganizationAnalyticsQueryHandler(
        IProgrammeMembershipRepository memberships,
        IVolunteerAssignmentRepository assignments,
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        IClock clock)
    {
        _memberships = memberships;
        _assignments = assignments;
        _polls = polls;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<OrganizationAnalyticsResponse>> Handle(
        GetOrganizationAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var permitted = await _access.RequireCapabilityAsync(
            query.OrganizationId, OrganizationCapability.ViewAnalytics, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<OrganizationAnalyticsResponse>(permitted.Error);
        }

        var period = AnalyticsWindow.Parse(query.Period);

        if (period is null)
        {
            return Result.Failure<OrganizationAnalyticsResponse>(
                CommunityProgrammeErrors.PeriodInvalid);
        }

        var now = _clock.UtcNow;
        var window = AnalyticsWindow.For(period.Value, now);

        // Sequential. Every one of these runs on the request's single scoped DbContext, and EF Core
        // refuses a second operation while the first is in flight — the defect Wave 4 found in both
        // Location aggregate endpoints, invisible on SQLite and real on Npgsql.
        var joins = await _memberships.FindJoinMomentsAsync(
            query.OrganizationId, window.From, now, cancellationToken);

        var confirmed = await _assignments.FindConfirmedForOrganizationAsync(
            query.OrganizationId, window.From, now, cancellationToken);

        var responses = await _polls.FindResponseMomentsAsync(
            query.OrganizationId, window.From, now, cancellationToken);

        return Result.Success(new OrganizationAnalyticsResponse
        {
            OrganizationId = query.OrganizationId,
            Period = AnalyticsWindow.Name(period.Value),
            MemberGrowth = AnalyticsWindow.Daily(joins, window),
            VolunteerHours = AnalyticsWindow.DailySum(
                confirmed.Select(assignment =>
                    (assignment.ConfirmedAt!.Value, assignment.ConfirmedHours ?? 0m)),
                window),
            PollParticipation = AnalyticsWindow.Daily(responses, window),
            ComputedAt = now
        });
    }
}

/// <summary>An organization's membership analytics, broken down by state.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ViewAnalytics)]
public sealed record GetMemberAnalyticsQuery : IQuery<MemberAnalyticsResponse>
{
    public required Guid OrganizationId { get; init; }

    public string? Period { get; init; }
}

public sealed class GetMemberAnalyticsQueryHandler
    : IQueryHandler<GetMemberAnalyticsQuery, MemberAnalyticsResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IClock _clock;

    public GetMemberAnalyticsQueryHandler(
        IProgrammeMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IClock clock)
    {
        _memberships = memberships;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<MemberAnalyticsResponse>> Handle(
        GetMemberAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var permitted = await _access.RequireCapabilityAsync(
            query.OrganizationId, OrganizationCapability.ViewAnalytics, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<MemberAnalyticsResponse>(permitted.Error);
        }

        var period = AnalyticsWindow.Parse(query.Period);

        if (period is null)
        {
            return Result.Failure<MemberAnalyticsResponse>(CommunityProgrammeErrors.PeriodInvalid);
        }

        var now = _clock.UtcNow;
        var window = AnalyticsWindow.For(period.Value, now);

        var byStatus = await _memberships.CountByStatusAsync(
            query.OrganizationId, cancellationToken);

        var joins = await _memberships.FindJoinMomentsAsync(
            query.OrganizationId, window.From, now, cancellationToken);

        int Count(ProgrammeMembershipStatus status) =>
            byStatus.TryGetValue(status, out var value) ? value : 0;

        return Result.Success(new MemberAnalyticsResponse
        {
            OrganizationId = query.OrganizationId,
            Period = AnalyticsWindow.Name(period.Value),
            TotalMembers = byStatus.Values.Sum(),
            ActiveMembers = Count(ProgrammeMembershipStatus.Active)
                + Count(ProgrammeMembershipStatus.RenewalDue),
            PendingApplications = Count(ProgrammeMembershipStatus.Pending),
            LapsedMembers = Count(ProgrammeMembershipStatus.Expired),
            ByStatus = byStatus.ToDictionary(
                entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal),
            Joins = AnalyticsWindow.Daily(joins, window),
            ComputedAt = now
        });
    }
}

/// <summary>
/// Community impact, over a scope and a period.
/// <para>
/// <b>Every metric carries its definition.</b> The register: "an undefined metric is worse than an
/// absent one". A member reading "14 hours volunteered" can see, on the same screen, that it counts
/// only hours an organizer confirmed.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetCommunityImpactQuery : IQuery<CommunityImpactResponse>
{
    /// <summary><c>member</c>, <c>organization</c>, <c>neighborhood</c> or <c>country</c>.</summary>
    public string? Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public string? Period { get; init; }
}

public sealed class GetCommunityImpactQueryHandler
    : IQueryHandler<GetCommunityImpactQuery, CommunityImpactResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IClock _clock;

    public GetCommunityImpactQueryHandler(
        IProgrammeMembershipRepository memberships,
        IVolunteerAssignmentRepository assignments,
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        IClock clock)
    {
        _memberships = memberships;
        _assignments = assignments;
        _polls = polls;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<CommunityImpactResponse>> Handle(
        GetCommunityImpactQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityImpactResponse>(caller.Error);
        }

        var period = AnalyticsWindow.Parse(query.Period);

        if (period is null)
        {
            return Result.Failure<CommunityImpactResponse>(CommunityProgrammeErrors.PeriodInvalid);
        }

        var scope = Enum.TryParse<ImpactScope>(query.Scope, ignoreCase: true, out var parsed)
            ? parsed
            : ImpactScope.Member;

        var now = _clock.UtcNow;
        var window = AnalyticsWindow.For(period.Value, now);

        // Only the member scope is served today, and the refusal for the others is honest rather than
        // a zero. An organization's impact is its members' plus its events' plus its campaigns', and
        // two of those three belong to modules that would have to publish a figure this one could
        // add up. Answering zeroes would put a number on a screen that nobody could reconcile — which
        // is the exact thing §34 forbids.
        if (scope != ImpactScope.Member)
        {
            return Result.Failure<CommunityImpactResponse>(CommunityProgrammeErrors.Validation(
                "Only a member's own impact is reported today. An organization's, a "
                + "neighbourhood's and a country's each need figures from modules that do not "
                + "publish one yet."));
        }

        var confirmed = await _assignments.FindConfirmedForVolunteerAsync(
            caller.Value, window.From, now, cancellationToken);

        var pollsAnswered = await _polls.CountResponsesForVoterAsync(
            caller.Value, window.From, now, cancellationToken);

        var memberships = await _memberships.FindForMemberAsync(caller.Value, 200, cancellationToken);

        var metrics = new List<ImpactMetricResponse>
        {
            new()
            {
                Key = "volunteerHours",
                Label = "Hours volunteered",
                Value = confirmed.Sum(assignment => assignment.ConfirmedHours ?? 0m),
                Definition =
                    "Hours an organizer confirmed after you checked in. Self-reported hours are "
                    + "never counted.",
                Source = "CommunityProgramme"
            },
            new()
            {
                Key = "organizationsSupported",
                Label = "Organizations supported",
                Value = confirmed
                    .Select(assignment => assignment.OrganizationId)
                    .Distinct()
                    .Count(),
                Definition =
                    "Distinct organizations you completed at least one confirmed volunteer "
                    + "assignment for in this period.",
                Source = "CommunityProgramme"
            },
            new()
            {
                Key = "pollsParticipated",
                Label = "Polls answered",
                Value = pollsAnswered,
                Definition =
                    "Polls you cast a vote in during this period. Anonymous polls are not counted, "
                    + "because they store no record of who voted.",
                Source = "CommunityProgramme"
            },
            new()
            {
                Key = "activeMemberships",
                Label = "Memberships held",
                Value = memberships.Count(membership => membership.GrantsBenefits),
                Definition =
                    "Community memberships currently active, including those inside their renewal "
                    + "grace period.",
                Source = "CommunityProgramme"
            }
        };

        return Result.Success(new CommunityImpactResponse
        {
            Scope = scope.ToString(),
            ScopeId = query.ScopeId,
            ScopeName = "You",
            Period = AnalyticsWindow.Name(period.Value),
            Metrics = metrics,
            ComputedAt = now
        });
    }
}

/// <summary>
/// The window an analytics read covers, and the daily buckets it fills.
/// <para>
/// <b>Every day in the window gets a point, including the empty ones.</b> A series that omitted quiet
/// days would render as a line joining two distant points, which reads as steady activity between
/// them — a chart lying about a fortnight nobody joined.
/// </para>
/// </summary>
internal static class AnalyticsWindow
{
    public static AnalyticsPeriod? Parse(string? period) => period?.Trim().ToLowerInvariant() switch
    {
        null or "" or "30d" => AnalyticsPeriod.Last30Days,
        "7d" => AnalyticsPeriod.Last7Days,
        "90d" => AnalyticsPeriod.Last90Days,
        "12m" => AnalyticsPeriod.Last12Months,
        "all" => AnalyticsPeriod.AllTime,
        _ => null
    };

    public static string Name(AnalyticsPeriod period) => period switch
    {
        AnalyticsPeriod.Last7Days => "7d",
        AnalyticsPeriod.Last90Days => "90d",
        AnalyticsPeriod.Last12Months => "12m",
        AnalyticsPeriod.AllTime => "all",
        _ => "30d"
    };

    public static Window For(AnalyticsPeriod period, DateTimeOffset now) => period switch
    {
        AnalyticsPeriod.Last7Days => new Window(now.AddDays(-7), 7),
        AnalyticsPeriod.Last90Days => new Window(now.AddDays(-90), 90),
        AnalyticsPeriod.Last12Months => new Window(now.AddMonths(-12), 365),

        // All time is bounded at two years of daily points. A chart with four thousand points is a
        // chart nobody can read and a payload nobody should send; the totals elsewhere are unbounded.
        AnalyticsPeriod.AllTime => new Window(now.AddYears(-2), 730),
        _ => new Window(now.AddDays(-30), 30)
    };

    /// <summary>One point per day, counting the moments that fall in it.</summary>
    public static IReadOnlyList<TimeSeriesPointResponse> Daily(
        IReadOnlyList<DateTimeOffset> moments, Window window)
    {
        var byDay = moments
            .GroupBy(moment => moment.UtcDateTime.Date)
            .ToDictionary(group => group.Key, group => (decimal)group.Count());

        return Fill(byDay, window);
    }

    /// <summary>One point per day, summing the values that fall in it.</summary>
    public static IReadOnlyList<TimeSeriesPointResponse> DailySum(
        IEnumerable<(DateTimeOffset Moment, decimal Value)> values, Window window)
    {
        var byDay = values
            .GroupBy(entry => entry.Moment.UtcDateTime.Date)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value));

        return Fill(byDay, window);
    }

    /// <summary>
    /// Fills the window's days, <b>including today</b>.
    /// <para>
    /// <b>The count is <c>Days</c>, and the window starts <c>Days - 1</c> days ago rather than
    /// <c>Days</c>.</b> The first version started a 30-day window thirty days back and filled thirty
    /// points, which ended yesterday — so a member who joined this morning contributed to no point on
    /// the chart, and the series summed to zero on a day something had happened. It errored nowhere:
    /// the request succeeded, the array was the right length, and every value was correct except the
    /// one that was missing.
    /// </para>
    /// </summary>
    private static List<TimeSeriesPointResponse> Fill(
        Dictionary<DateTime, decimal> byDay, Window window)
    {
        var points = new List<TimeSeriesPointResponse>(window.Days);
        var day = window.From.UtcDateTime.Date.AddDays(1);

        for (var index = 0; index < window.Days; index++)
        {
            points.Add(new TimeSeriesPointResponse
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Value = byDay.TryGetValue(day, out var value) ? value : 0m
            });

            day = day.AddDays(1);
        }

        return points;
    }

    internal readonly record struct Window(DateTimeOffset From, int Days);
}

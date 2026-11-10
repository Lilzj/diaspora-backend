using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// Cohort retention, counted from recorded activity — FRONTEND-PHASE15-GAP-004.
/// <para>
/// <b>Subjects.</b> The platform (platform readers only — the client draws this table as an
/// administrative view), the caller's own work as <c>creator</c> or in one role (<c>seller</c>,
/// <c>landlord</c>, <c>employer</c>, <c>provider</c>), or one item the caller owns. For the platform a
/// person is anybody whose activity was recorded; for an owner or an item, anybody who opened or acted
/// on it. Communities, countries, neighbourhoods and campaigns are refused with the reason: nothing in
/// the store is keyed by them.
/// </para>
/// <para>
/// <b>Bounded by what is kept.</b> Raw activity is kept for
/// <see cref="AnalyticsOptions.RawEventRetentionDays"/> days; a cohort that starts before then is
/// listed in <c>unavailableCohorts</c>, never estimated.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsRetentionQuery : IQuery<AnalyticsRetentionTableResponse>
{
    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    /// <summary><c>day</c>, <c>week</c> (the default) or <c>month</c>.</summary>
    public string? CohortGranularity { get; init; }

    /// <summary>1 to 24; 8 when absent.</summary>
    public int? CohortCount { get; init; }

    /// <summary>The last day counted, <c>YYYY-MM-DD</c>. Today when absent; never later than today.</summary>
    public string? To { get; init; }

    /// <summary>Validated, but days are always the platform's reporting days — see the response.</summary>
    public string? Timezone { get; init; }
}

public sealed class GetAnalyticsRetentionQueryHandler
    : IQueryHandler<GetAnalyticsRetentionQuery, AnalyticsRetentionTableResponse>
{
    private const int DefaultCohorts = 8;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsActivitySubjects _subjects;
    private readonly IAnalyticsEventRepository _events;
    private readonly AnalyticsActorHasher _actors;
    private readonly AnalyticsOptions _options;

    public GetAnalyticsRetentionQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsActivitySubjects subjects,
        IAnalyticsEventRepository events,
        AnalyticsActorHasher actors,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _subjects = subjects;
        _events = events;
        _actors = actors;
        _options = options.Value;
    }

    public async Task<Result<AnalyticsRetentionTableResponse>> Handle(
        GetAnalyticsRetentionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsRetentionTableResponse>(caller.Error);
        }

        AnalyticsCohortGranularity? granularity = query.CohortGranularity?.Trim().ToLowerInvariant() switch
        {
            null or "" or "week" => AnalyticsCohortGranularity.Week,
            "day" => AnalyticsCohortGranularity.Day,
            "month" => AnalyticsCohortGranularity.Month,
            _ => null
        };

        if (granularity is null)
        {
            return Result.Failure<AnalyticsRetentionTableResponse>(AnalyticsErrors.RetentionGranularityUnsupported);
        }

        var cohortCount = query.CohortCount ?? DefaultCohorts;

        if (cohortCount < 1 || cohortCount > AnalyticsCohortTable.MaximumCohorts)
        {
            return Result.Failure<AnalyticsRetentionTableResponse>(
                AnalyticsErrors.RetentionCohortCountInvalid(AnalyticsCohortTable.MaximumCohorts));
        }

        if (!string.IsNullOrWhiteSpace(query.Timezone)
            && AnalyticsReportingCalendar.TryResolve(query.Timezone) is null)
        {
            return Result.Failure<AnalyticsRetentionTableResponse>(AnalyticsErrors.TimezoneUnknown);
        }

        var today = _context.Calendar.Today;
        var endDay = today;

        if (!string.IsNullOrWhiteSpace(query.To))
        {
            if (!DateOnly.TryParse(query.To, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            {
                return Result.Failure<AnalyticsRetentionTableResponse>(AnalyticsErrors.RangeIncomplete);
            }

            endDay = to > today ? today : to;
        }

        var subject = await _subjects
            .ResolveAsync(query.SubjectType, query.SubjectId, caller.Value, allowItems: true, "platform", cancellationToken)
            .ConfigureAwait(false);

        if (subject.IsFailure)
        {
            return Result.Failure<AnalyticsRetentionTableResponse>(subject.Error);
        }

        var now = _context.Calendar.UtcNow;

        // The first whole day the store still holds. The retention sweep deletes by instant, so the
        // day it is part-way through is not counted as held.
        var horizon = _context.Calendar.DayOf(now.AddDays(-_options.RawEventRetentionDays)).AddDays(1);

        IReadOnlyList<(string ActorKey, DateOnly Day)> activity = horizon > endDay
            ? []
            : await _events
                .ListActorDaysAsync(subject.Value.Scope, horizon, endDay, cancellationToken)
                .ConfigureAwait(false);

        // The owner's own publishing and visits are recorded against their work; they are not its
        // audience, and would otherwise sit in every cohort as somebody who always comes back.
        if (subject.Value.SelfUserId is { } self)
        {
            var selfKey = _actors.Hash(self);
            activity = activity.Where(pair => !string.Equals(pair.ActorKey, selfKey, StringComparison.Ordinal)).ToList();
        }

        var table = AnalyticsCohortTable.Build(activity, granularity.Value, cohortCount, endDay, horizon);
        var zone = _context.Calendar.TimeZoneId;
        var horizonText = horizon.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        return Result.Success(new AnalyticsRetentionTableResponse
        {
            Subject = subject.Value.Subject,
            Granularity = granularity.Value switch
            {
                AnalyticsCohortGranularity.Day => "day",
                AnalyticsCohortGranularity.Week => "week",
                _ => "month"
            },
            OffsetLabels = AnalyticsCohortTable.OffsetLabels(granularity.Value),
            MeasurementWindow = MeasurementWindow(granularity.Value, subject.Value.Kind, zone),
            Timezone = zone,
            Cohorts =
            [
                .. table.Cohorts.Select(cohort => new AnalyticsRetentionCohortResponse
                {
                    CohortDate = cohort.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    CohortSize = cohort.Size,
                    Values = cohort.Values,
                    IsIncomplete = cohort.IsIncomplete
                })
            ],
            UnavailableCohorts =
            [
                .. table.Unavailable.Select(gap => new AnalyticsUnavailableCohortResponse
                {
                    CohortDate = gap.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Reason = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Activity is kept for {_options.RawEventRetentionDays} days, so who was new in a period that began before {horizonText} can no longer be told.")
                })
            ],
            Source = "backend",
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = "Diaspora analytics",
                Methodology = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Counted at request time from recorded activity between {horizonText} and {endDay:yyyy-MM-dd}: each person once per day, followed from the period they were first recorded in. Days are cut in {zone}."),
                Limitations =
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Activity is kept for {_options.RawEventRetentionDays} days, so somebody active before {horizonText} and again since is counted as new at their first activity since then."),
                    "Only signed-in activity can be followed from one period to the next; activity without a member is not counted.",
                    "A cohort's own period counts as retained by definition, so the first column is always one where the cohort is not empty."
                ],
                SampleSize = table.Cohorts.Sum(cohort => cohort.Size)
            }
        });
    }

    private static string MeasurementWindow(
        AnalyticsCohortGranularity granularity, AnalyticsActivitySubjectKind kind, string zone)
    {
        var activity = kind switch
        {
            AnalyticsActivitySubjectKind.Platform => "anything they did on Diaspora was recorded",
            AnalyticsActivitySubjectKind.Owner => "they were recorded opening or acting on this work",
            _ => "they were recorded opening or acting on this item"
        };

        return granularity switch
        {
            AnalyticsCohortGranularity.Day => string.Create(
                CultureInfo.InvariantCulture,
                $"A person counts as retained on day N if {activity} at any point that calendar day, in {zone}. A cohort is the people first recorded that day."),
            AnalyticsCohortGranularity.Week => string.Create(
                CultureInfo.InvariantCulture,
                $"A person counts as retained in week N if {activity} at least once that week. Weeks begin on Monday, in {zone}. A cohort is the people first recorded that week."),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"A person counts as retained in month N if {activity} at least once that calendar month, in {zone}. A cohort is the people first recorded that month.")
        };
    }
}

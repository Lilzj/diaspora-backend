using System.Globalization;
using Diaspora.Contracts.Community.V1.Activity;
using Diaspora.Contracts.CommunityLife.V1.Activity;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>A statistics window: the period's name and the <c>[From, Until)</c> it covers.</summary>
public sealed record StatisticsWindow(string Period, DateTimeOffset From, DateTimeOffset Until, string Interval)
{
    /// <summary>The window of the same length immediately before, or null for <c>all</c>.</summary>
    public (DateTimeOffset From, DateTimeOffset Until)? Previous =>
        Period == "all" ? null : (From - (Until - From), From);
}

/// <summary>
/// A community's figures for a period — FRONTEND-PHASE13-GAP-007 and -008, 21 Oct 2026.
/// <para>
/// <b>Every figure is a count of stored rows; nothing is estimated.</b> The development adapter this
/// replaces invented a member count, a growth rate and an engagement figure. Here the member figures
/// come from <c>community_memberships</c>, the notice and rule figures from this module's own tables,
/// and the activity figures from Community and Community Life through count-only contracts. What the
/// platform cannot count is named in <c>unavailableMetrics</c> with the reason, never shown as zero.
/// </para>
/// <para>
/// <b>Activity is what the community's members did, not what was done "in" the community.</b> No post
/// or event on the platform carries a community, so that is the only honest reading — and every
/// activity metric's methodology says so in the sentence the screen prints.
/// </para>
/// <para>
/// <b>Activity is restricted, and suppressed below <see cref="MinimumCohort"/> members.</b> A count of
/// what a handful of members posted can identify which accounts are members, including ones who kept
/// their membership private. So activity is served only to callers holding
/// <c>canViewPrivateStatistics</c>, and not at all for a community too small to hide anybody in.
/// </para>
/// <para>
/// A service rather than a handler both queries dispatch, because a handler that can reach another
/// handler can reach one whose authorization it has not performed (DSP1001).
/// </para>
/// </summary>
public sealed class CommunityStatisticsComposer
{
    /// <summary>Below this many current members, activity is not reported at all.</summary>
    public const int MinimumCohort = 5;

    /// <summary>The members whose activity is counted. A community larger than this is counted on its oldest members.</summary>
    private const int ActivityMemberCap = 5000;

    private const string SourceName = "Diaspora community records";

    private static readonly string[] Periods = ["7d", "30d", "90d", "12m", "all"];

    private readonly ICommunityMembershipStatistics _memberships;
    private readonly ICommunityNoticeRepository _notices;
    private readonly ICommunityRuleRepository _rules;
    private readonly IMemberPostActivityReader _posts;
    private readonly IMemberEventActivityReader _events;

    public CommunityStatisticsComposer(
        ICommunityMembershipStatistics memberships,
        ICommunityNoticeRepository notices,
        ICommunityRuleRepository rules,
        IMemberPostActivityReader posts,
        IMemberEventActivityReader events)
    {
        _memberships = memberships;
        _notices = notices;
        _rules = rules;
        _posts = posts;
        _events = events;
    }

    public static Result<string> ParsePeriod(string? period)
    {
        var value = string.IsNullOrWhiteSpace(period) ? "30d" : period.Trim().ToLowerInvariant();

        return Periods.Contains(value, StringComparer.Ordinal)
            ? Result.Success(value)
            : Result.Failure<string>(CommunityGovernanceErrors.StatisticsPeriodUnknown);
    }

    public static StatisticsWindow WindowFor(string period, CommunityAggregate community, DateTimeOffset now)
    {
        var from = period switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            "12m" => now.AddMonths(-12),

            // Since the community began. CreatedAt is stamped by the context on insert; a seeded row
            // that somehow lacks it falls back to a year, which the window then says plainly.
            _ => community.CreatedAt == default || community.CreatedAt > now
                ? now.AddMonths(-12)
                : community.CreatedAt
        };

        var interval = period switch
        {
            "7d" or "30d" => "day",
            "90d" => "week",
            _ => "month"
        };

        return new StatisticsWindow(period, from, now, interval);
    }

    public async Task<CommunityStatisticsResponse> ComposeAsync(
        CommunityAggregate community,
        StatisticsWindow window,
        bool includeRestricted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(community);
        ArgumentNullException.ThrowIfNull(window);

        var now = window.Until;
        var metrics = new List<CommunityMetricResponse>();
        var unavailable = new List<UnavailableMetricResponse>();

        // ── Membership, from the record of who belongs ───────────────────────────────────────
        // Sequential throughout: one scoped DbContext, and EF Core refuses a second operation on it.

        var members = await _memberships.CountCurrentMembersAsync(community.Id, cancellationToken);

        metrics.Add(Metric(
            "members", "Members", members, "members", window, now,
            "Current members: everybody whose membership is active today, whatever its visibility.",
            restricted: false));

        var joins = await _memberships.ListJoinTimesAsync(
            community.Id, window.From, window.Until, cancellationToken);

        int? previousJoins = null;

        if (window.Previous is { } previous)
        {
            previousJoins = (await _memberships.ListJoinTimesAsync(
                community.Id, previous.From, previous.Until, cancellationToken)).Count;
        }

        metrics.Add(Metric(
            "newMembers", "New members", joins.Count, "members", window, now,
            "Memberships that began in the period, including any that have since ended. A member "
            + "who rejoined counts once, at their latest join.",
            restricted: false,
            change: ChangePercent(joins.Count, previousJoins)));

        if (includeRestricted)
        {
            var departures = await _memberships.CountDeparturesAsync(
                community.Id, window.From, window.Until, cancellationToken);

            metrics.Add(Metric(
                "departures", "Members who left", departures, "members", window, now,
                "Memberships that ended in the period, by leaving or by removal.",
                restricted: true));
        }

        // ── What the community said, from this module's own tables ───────────────────────────

        var notices = await _notices.CountPublishedAsync(
            community.Id, window.From, window.Until, cancellationToken);

        metrics.Add(Metric(
            "notices", "Notices published", notices, "notices", window, now,
            "Notices the people who run the community published in the period.",
            restricted: false));

        var rules = await _rules.CountAsync(community.Id, cancellationToken);

        metrics.Add(Metric(
            "rules", "Rules", rules, "rules", window, now,
            "Rules the community publishes today. Not tied to the period.",
            restricted: false));

        // ── What the members did elsewhere on the platform ───────────────────────────────────

        if (includeRestricted)
        {
            if (members < MinimumCohort)
            {
                foreach (var (key, label) in ActivityMetrics)
                {
                    unavailable.Add(new UnavailableMetricResponse
                    {
                        Key = key,
                        Label = label,
                        Reason = $"Not reported for a community of fewer than {MinimumCohort} members: "
                            + "a figure that small could identify who belongs."
                    });
                }
            }
            else
            {
                var memberIds = await _memberships.ListCurrentMemberIdsAsync(
                    community.Id, ActivityMemberCap, cancellationToken);

                var posts = await _posts.CountAsync(memberIds, window.From, window.Until, cancellationToken);
                var events = await _events.CountAsync(memberIds, window.From, window.Until, cancellationToken);

                var scope = members > ActivityMemberCap
                    ? $" Counted over the community's {ActivityMemberCap:N0} longest-standing members."
                    : string.Empty;

                metrics.Add(Metric(
                    "posts", "Posts by members", posts.PostCount, "posts", window, now,
                    "Feed posts the community's current members published in the period, anywhere on "
                    + "Diaspora. No post carries a community, so this is what members did, not what was "
                    + "posted to the community." + scope,
                    restricted: true));

                metrics.Add(Metric(
                    "activeMembers", "Members who posted", posts.AuthorCount, "members", window, now,
                    "Current members who published at least one feed post in the period, anywhere on "
                    + "Diaspora." + scope,
                    restricted: true));

                metrics.Add(Metric(
                    "events", "Events by members", events.EventCount, "events", window, now,
                    "Events the community's current members first published in the period, anywhere "
                    + "on Diaspora. No event carries a community." + scope,
                    restricted: true));
            }
        }

        // ── What nothing on the platform can count for a community ───────────────────────────

        foreach (var (key, label) in UncountableContent)
        {
            unavailable.Add(new UnavailableMetricResponse
            {
                Key = key,
                Label = label,
                Reason = "Nothing of this kind on Diaspora names a community, so none can be counted for one."
            });
        }

        return new CommunityStatisticsResponse
        {
            CommunityId = community.Id.Value,
            Period = window.Period,
            From = window.From,
            Until = window.Until,
            Metrics = metrics,
            UnavailableMetrics = unavailable,
            Series =
            [
                new StatisticsSeriesResponse
                {
                    Key = "newMembers",
                    Label = "New members",
                    Interval = window.Interval,
                    Points = Bucket(joins, window)
                }
            ],
            Source = "backend",
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = SourceName,
                Methodology = "Counted from stored records when requested. Nothing is estimated.",
                SampleSize = members
            }
        };
    }

    /// <summary>The activity metrics, named when they cannot be served.</summary>
    private static readonly (string Key, string Label)[] ActivityMetrics =
    [
        ("posts", "Posts by members"),
        ("activeMembers", "Members who posted"),
        ("events", "Events by members")
    ];

    /// <summary>Content the development adapter counted by guessing from a publisher's origin.</summary>
    private static readonly (string Key, string Label)[] UncountableContent =
    [
        ("guides", "Guides"),
        ("jobs", "Jobs"),
        ("housing", "Housing"),
        ("marketplace", "Marketplace"),
        ("organizations", "Organisations")
    ];

    private static CommunityMetricResponse Metric(
        string key,
        string label,
        int value,
        string unit,
        StatisticsWindow window,
        DateTimeOffset now,
        string methodology,
        bool restricted,
        double? change = null) => new()
    {
        Key = key,
        Label = label,
        Value = SourcedResponse<int>.Backed(value),
        DisplayValue = value.ToString("N0", CultureInfo.InvariantCulture),
        Unit = unit,
        ChangePercent = change is { } percent
            ? new SourcedResponse<double> { Value = percent, Source = "backend" }
            : null,
        Period = window.Period,
        IsRestricted = restricted,
        Freshness = new DataFreshnessResponse
        {
            ComputedAt = now,
            SourceName = SourceName,
            Methodology = methodology
        }
    };

    /// <summary>Change against the previous window, or null where there is none to compare with.</summary>
    private static double? ChangePercent(int current, int? previous) =>
        previous is > 0 ? Math.Round((current - previous.Value) * 100d / previous.Value, 1) : null;

    private static List<StatisticsPointResponse> Bucket(
        IReadOnlyList<DateTimeOffset> times, StatisticsWindow window)
    {
        var starts = new List<DateTimeOffset>();

        for (var start = window.From; start < window.Until; start = Step(start, window.Interval))
        {
            starts.Add(start);
        }

        var counts = new int[starts.Count];

        foreach (var time in times)
        {
            // The last bucket whose start is at or before the time. Starts are ascending.
            var index = starts.FindLastIndex(start => start <= time);

            if (index >= 0)
            {
                counts[index]++;
            }
        }

        return [.. starts.Select((start, index) => new StatisticsPointResponse
        {
            Start = start,
            Value = counts[index]
        })];
    }

    private static DateTimeOffset Step(DateTimeOffset start, string interval) => interval switch
    {
        "day" => start.AddDays(1),
        "week" => start.AddDays(7),
        _ => start.AddMonths(1)
    };
}

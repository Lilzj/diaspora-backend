using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// Statements the platform can make about a subject's counted figures — FRONTEND-PHASE15-GAP-028.
/// <para>
/// <b>Arithmetic, stated with its figures, and nothing else.</b> Each insight compares two counted
/// totals — this period against the one before, one item against the rest, saves or enquiries against
/// views — and its detail names the numbers it rests on. None predicts, scores, or says why. A
/// statement over a handful of views would describe a few people's behaviour, so each one has a floor
/// below which it is not made; an empty list is an honest answer.
/// </para>
/// <para>
/// <b>Not produced, and why</b>: a search no-result rate (searches are not recorded per subject),
/// reach outside a member's own community (activity is not recorded per community), and day-of-week
/// patterns (a period's days are too few to tell a pattern from chance).
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsInsightsQuery : IQuery<IReadOnlyList<AnalyticsInsightResponse>>
{
    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetAnalyticsInsightsQueryHandler
    : IQueryHandler<GetAnalyticsInsightsQuery, IReadOnlyList<AnalyticsInsightResponse>>
{
    /// <summary>Below this many views in the earlier period, a percentage change is not stated.</summary>
    public const long MinimumBaselineViews = 10;

    /// <summary>Below this many views in the period, no share or ratio is stated.</summary>
    public const long MinimumViews = 20;

    /// <summary>A change smaller than this either way is "held steady".</summary>
    internal const double SteadyBand = 0.05;

    /// <summary>One item has to draw at least this share of the views to be called out.</summary>
    internal const double DominantShare = 0.5;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsActivitySubjects _subjects;
    private readonly AnalyticsItemReads _items;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public GetAnalyticsInsightsQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsActivitySubjects subjects,
        AnalyticsItemReads items,
        IAnalyticsContentDimensionRepository dimensions)
    {
        _context = context;
        _subjects = subjects;
        _items = items;
        _dimensions = dimensions;
    }

    public async Task<Result<IReadOnlyList<AnalyticsInsightResponse>>> Handle(
        GetAnalyticsInsightsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AnalyticsInsightResponse>>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"), query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AnalyticsInsightResponse>>(resolved.Error);
        }

        var subject = await _subjects
            .ResolveAsync(query.SubjectType, query.SubjectId, caller.Value, allowItems: true, "creator", cancellationToken)
            .ConfigureAwait(false);

        if (subject.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AnalyticsInsightResponse>>(subject.Error);
        }

        var period = resolved.Value;
        var baseline = period.Previous();

        var current = await _items.ReadAsync(subject.Value, period, cancellationToken).ConfigureAwait(false);
        var previous = await _items.ReadAsync(subject.Value, baseline, cancellationToken).ConfigureAwait(false);

        var insights = new List<AnalyticsInsightResponse>();
        var whose = subject.Value.Kind switch
        {
            AnalyticsActivitySubjectKind.Platform => "content on Diaspora",
            AnalyticsActivitySubjectKind.Content => "this item",
            _ => "your work"
        };

        var views = current.Sum(item => item.Views);
        var earlierViews = previous.Sum(item => item.Views);
        var days = period.ToDate.DayNumber - period.FromDate.DayNumber + 1;

        if (ViewsChange(views, earlierViews, days, baseline, whose) is { } change)
        {
            insights.Add(change);
        }

        // Stated only where the change itself is not: from ten views or more, "fell 100%" says it.
        if (views == 0 && earlierViews is > 0 and < MinimumBaselineViews)
        {
            insights.Add(new AnalyticsInsightResponse
            {
                Key = "quiet-period",
                Headline = string.Create(CultureInfo.InvariantCulture, $"Nothing of {whose} was opened in this period"),
                Detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{earlierViews:N0} {Plural(earlierViews, "view", "views")} in the {days} days before, {Range(baseline)}."),
                Tone = "neutral",
                Source = "backend"
            });
        }

        if (subject.Value.Kind != AnalyticsActivitySubjectKind.Content
            && await DominantItemAsync(current, views, whose, cancellationToken).ConfigureAwait(false) is { } dominant)
        {
            insights.Add(dominant);
        }

        var saves = current.Sum(item => item.Saves);
        var enquiries = current.Sum(item => item.Enquiries);

        if (Ratio("saves-per-view", "save", saves, views, period) is { } savesInsight)
        {
            insights.Add(savesInsight);
        }

        if (Ratio("enquiries-per-view", "enquiry", enquiries, views, period) is { } enquiriesInsight)
        {
            insights.Add(enquiriesInsight);
        }

        return Result.Success<IReadOnlyList<AnalyticsInsightResponse>>(insights);
    }

    public static AnalyticsInsightResponse? ViewsChange(
        long views, long earlierViews, int days, AnalyticsPeriod baseline, string whose)
    {
        if (earlierViews < MinimumBaselineViews)
        {
            return null;
        }

        var change = (views - earlierViews) / (double)earlierViews;
        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"{views:N0} {Plural(views, "view", "views")} of {whose}, against {earlierViews:N0} in the {days} days before ({Range(baseline)}).");

        if (Math.Abs(change) < SteadyBand)
        {
            return new AnalyticsInsightResponse
            {
                Key = "views-steady",
                Headline = string.Create(CultureInfo.InvariantCulture, $"Views held steady against the previous {days} days"),
                Detail = detail,
                Tone = "neutral",
                Source = "backend"
            };
        }

        var percent = Math.Round(Math.Abs(change) * 100, MidpointRounding.AwayFromZero);

        return new AnalyticsInsightResponse
        {
            Key = change > 0 ? "views-rose" : "views-fell",
            Headline = string.Create(
                CultureInfo.InvariantCulture,
                $"Views {(change > 0 ? "rose" : "fell")} {percent:0}% against the previous {days} days"),
            Detail = detail,
            Tone = change > 0 ? "positive" : "negative",
            Source = "backend"
        };
    }

    public static AnalyticsInsightResponse? Ratio(
        string key, string noun, long count, long views, AnalyticsPeriod period)
    {
        if (views < MinimumViews || count <= 0)
        {
            return null;
        }

        var every = Math.Max(1, (long)Math.Round(views / (double)count, MidpointRounding.AwayFromZero));

        return new AnalyticsInsightResponse
        {
            Key = key,
            Headline = every == 1
                ? string.Create(CultureInfo.InvariantCulture, $"About one {noun} for every view")
                : string.Create(CultureInfo.InvariantCulture, $"About one {noun} for every {every:N0} views"),
            Detail = string.Create(
                CultureInfo.InvariantCulture,
                $"{count:N0} {Plural(count, noun, noun == "enquiry" ? "enquiries" : noun + "s")} and {views:N0} views, {Range(period)}. The two are counted separately: this compares totals, it does not follow one person from opening to acting."),
            Tone = "neutral",
            Source = "backend"
        };
    }

    private async Task<AnalyticsInsightResponse?> DominantItemAsync(
        IReadOnlyList<AnalyticsItemTotals> items, long views, string whose, CancellationToken cancellationToken)
    {
        var viewed = items.Where(item => item.Views > 0).ToList();

        if (viewed.Count < 2 || views < MinimumViews)
        {
            return null;
        }

        var top = viewed
            .OrderByDescending(item => item.Views)
            .ThenBy(item => item.ContentId)
            .First();

        var share = top.Views / (double)views;

        if (share < DominantShare)
        {
            return null;
        }

        var dimension = await _dimensions
            .FindAsync(top.ContentType, top.ContentId, cancellationToken)
            .ConfigureAwait(false);

        var title = dimension?.Title ?? "One item";
        var percent = Math.Round(share * 100, MidpointRounding.AwayFromZero);

        return new AnalyticsInsightResponse
        {
            Key = "one-item-leads",
            Headline = string.Create(
                CultureInfo.InvariantCulture, $"One item drew {percent:0}% of the views of {whose}"),
            Detail = string.Create(
                CultureInfo.InvariantCulture,
                $"\"{title}\": {top.Views:N0} of {views:N0} views, across {viewed.Count} items that were opened."),
            Tone = "neutral",
            Action = Domain.Policies.AnalyticsContentTypes.ToClientRoute(top.ContentType, top.ContentId) is { } route
                ? new AnalyticsInsightActionResponse { Label = "Open it", Route = route }
                : null,
            Source = "backend"
        };
    }

    private static string Range(AnalyticsPeriod period) => string.Create(
        CultureInfo.InvariantCulture,
        $"{period.FromDate:d MMM yyyy} to {period.ToDate:d MMM yyyy}");

    private static string Plural(long count, string one, string many) => count == 1 ? one : many;
}

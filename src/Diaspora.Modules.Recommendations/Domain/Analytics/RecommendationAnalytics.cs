using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Analytics;

/// <summary>
/// One day's measured performance for one surface.
/// <para>
/// <b>A rollup, not an event log, and the difference is what makes the analytics endpoint
/// answerable.</b> "What is the acceptance rate on the jobs surface this month" over raw feedback
/// rows is an aggregate scan across the largest table in the schema; over rollups it is thirty
/// indexed reads. The feedback rows remain the record — the rollup is derived and can be rebuilt
/// from them, which is what makes it safe to sweep.
/// </para>
/// <para>
/// <b>The measures were chosen from the brief and then one was added.</b> Views, clicks,
/// acceptances, dismissals, cold-start counts, distinct targets and diversity are all named there.
/// <see cref="ProfileCompletenessSum"/> was not, and it earns its place because every other number
/// here is uninterpretable without it: a 2% acceptance rate on a surface whose members average 15%
/// profile completeness is a cold-start problem, and the same number at 80% completeness is a
/// ranking problem. They call for opposite responses.
/// </para>
/// <para>
/// Sums rather than averages are stored, because averages cannot be combined. Two days at 40% and
/// 60% do not make a week at 50% unless both days had the same traffic, and they never do.
/// </para>
/// </summary>
public sealed class RecommendationAnalytics : AggregateRoot<RecommendationAnalyticsId>, IAuditableEntity
{
    private readonly List<AnalyticsBreakdown> _breakdowns = [];

    private RecommendationAnalytics(
        RecommendationAnalyticsId id,
        DateOnly day,
        RecommendationSurface surface,
        DateTimeOffset createdAt) : base(id)
    {
        Day = day;
        Surface = surface;
        CreatedAt = createdAt;
    }

    private RecommendationAnalytics() { }

    /// <summary>UTC. A day boundary in the member's timezone would make two surfaces incomparable.</summary>
    public DateOnly Day { get; private set; }

    public RecommendationSurface Surface { get; private set; }

    public long GeneratedSetCount { get; private set; }

    public long GeneratedItemCount { get; private set; }

    public long ViewCount { get; private set; }

    public long ClickCount { get; private set; }

    public long AcceptCount { get; private set; }

    public long DismissCount { get; private set; }

    public long BookmarkCount { get; private set; }

    /// <summary>How many of the day's sets came from the cold-start path.</summary>
    public long ColdStartSetCount { get; private set; }

    /// <summary>How many of those cold-start sets produced at least one acceptance.</summary>
    public long ColdStartAcceptCount { get; private set; }

    /// <summary>How many candidates policies removed. The number that explains a thin page.</summary>
    public long PolicyExclusionCount { get; private set; }

    /// <summary>Distinct targets recommended across the day. The numerator of diversity.</summary>
    public long DistinctTargetCount { get; private set; }

    /// <summary>Summed profile completeness at generation time. See the class summary.</summary>
    public long ProfileCompletenessSum { get; private set; }

    /// <summary>Summed generation latency in milliseconds. Divided by set count for the mean.</summary>
    public long GenerationMillisecondsSum { get; private set; }

    /// <summary>The slowest single generation of the day. A mean hides exactly the thing worth seeing.</summary>
    public int GenerationMillisecondsMax { get; private set; }

    /// <summary>Summed signal age in hours at generation time. Answers "is the profile keeping up".</summary>
    public long SignalFreshnessHoursSum { get; private set; }

    public IReadOnlyList<AnalyticsBreakdown> Breakdowns => _breakdowns.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RecommendationAnalytics Open(
        RecommendationAnalyticsId id,
        DateOnly day,
        RecommendationSurface surface,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new RecommendationAnalytics(id, day, surface, createdAt);
    }

    public void RecordGeneration(
        int itemCount,
        bool isColdStart,
        int policyExclusionCount,
        int profileCompleteness,
        int generationMilliseconds,
        int signalFreshnessHours)
    {
        GeneratedSetCount++;
        GeneratedItemCount += Math.Max(0, itemCount);
        PolicyExclusionCount += Math.Max(0, policyExclusionCount);
        ProfileCompletenessSum += Math.Clamp(profileCompleteness, 0, 100);
        GenerationMillisecondsSum += Math.Max(0, generationMilliseconds);
        SignalFreshnessHoursSum += Math.Max(0, signalFreshnessHours);

        GenerationMillisecondsMax = Math.Max(GenerationMillisecondsMax, Math.Max(0, generationMilliseconds));

        if (isColdStart)
        {
            ColdStartSetCount++;
        }
    }

    public void RecordFeedback(RecommendationFeedbackKind kind, bool fromColdStartSet)
    {
        switch (kind)
        {
            case RecommendationFeedbackKind.Viewed:
                ViewCount++;
                break;
            case RecommendationFeedbackKind.Clicked:
                ClickCount++;
                break;
            case RecommendationFeedbackKind.Accepted:
                AcceptCount++;
                if (fromColdStartSet) ColdStartAcceptCount++;
                break;
            case RecommendationFeedbackKind.Dismissed:
                DismissCount++;
                break;
            case RecommendationFeedbackKind.Bookmarked:
                BookmarkCount++;
                break;
            default:
                return;
        }
    }

    public void SetDistinctTargets(long count) => DistinctTargetCount = Math.Max(0, count);

    /// <summary>Adds to a slice, creating it on first sight.</summary>
    public AnalyticsBreakdown Slice(string dimension, string key, Guid breakdownId)
    {
        var normalizedDimension = dimension.Trim().ToLowerInvariant();
        var normalizedKey = key.Trim().ToLowerInvariant();

        var existing = _breakdowns.Find(breakdown =>
            string.Equals(breakdown.Dimension, normalizedDimension, StringComparison.Ordinal) &&
            string.Equals(breakdown.Key, normalizedKey, StringComparison.Ordinal));

        if (existing is not null)
        {
            return existing;
        }

        var created = AnalyticsBreakdown.Create(breakdownId, normalizedDimension, normalizedKey);
        _breakdowns.Add(created);

        return created;
    }

    // ── Derived, for the reader ─────────────────────────────────────────────────────────────

    public decimal AcceptanceRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)AcceptCount / ViewCount, 4);

    public decimal ClickThroughRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)ClickCount / ViewCount, 4);

    public decimal DismissalRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)DismissCount / ViewCount, 4);

    /// <summary>
    /// Distinct targets over items shown. One means every card was different.
    /// <para>
    /// The measure the brief calls "recommendation diversity". A surface whose diversity falls
    /// towards zero is showing the same twenty things to everybody, which is the failure mode a
    /// popularity-weighted ranking drifts into on its own.
    /// </para>
    /// </summary>
    public decimal DiversityScore =>
        GeneratedItemCount == 0 ? 0m : decimal.Round((decimal)DistinctTargetCount / GeneratedItemCount, 4);

    /// <summary>Cold-start sets that led to an acceptance, over cold-start sets.</summary>
    public decimal ColdStartSuccessRate =>
        ColdStartSetCount == 0 ? 0m : decimal.Round((decimal)ColdStartAcceptCount / ColdStartSetCount, 4);

    public decimal AverageProfileCompleteness =>
        GeneratedSetCount == 0 ? 0m : decimal.Round((decimal)ProfileCompletenessSum / GeneratedSetCount, 2);

    public decimal AverageGenerationMilliseconds =>
        GeneratedSetCount == 0 ? 0m : decimal.Round((decimal)GenerationMillisecondsSum / GeneratedSetCount, 2);

    public decimal AverageSignalFreshnessHours =>
        GeneratedSetCount == 0 ? 0m : decimal.Round((decimal)SignalFreshnessHoursSum / GeneratedSetCount, 2);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}

using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// What a member has done with one kind of thing.
/// <para>
/// <b>A counter set, maintained for one query that would otherwise scan a member's whole signal
/// history.</b> "Does this member care about housing" is asked on every home-feed request, and
/// answering it by counting signals is a scan whose cost grows with how long somebody has been a
/// member — so the platform's most engaged members would get the slowest feed, which is exactly
/// backwards. It is the same trade B13 made for the unread badge, for the same reason.
/// </para>
/// <para>
/// <see cref="DismissCount"/> is here rather than in the feedback table because it is read at the
/// same instant as the rest: a member who has dismissed nine of the last ten marketplace
/// recommendations should not be shown a tenth, and that judgement should not cost a second query.
/// </para>
/// </summary>
public sealed class ProfileActivitySummary : ValueObject
{
    private ProfileActivitySummary(Guid summaryId, RecommendationTargetType targetType)
    {
        SummaryId = summaryId;
        TargetType = targetType;
    }

    private ProfileActivitySummary() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid SummaryId { get; private set; }

    public RecommendationTargetType TargetType { get; private set; }

    public int ViewCount { get; private set; }

    public int SaveCount { get; private set; }

    /// <summary>Applications, enquiries, viewing requests — the "I acted on it" count.</summary>
    public int ApplyCount { get; private set; }

    /// <summary>Offers, reservations, purchases, hires.</summary>
    public int TransactCount { get; private set; }

    public int DismissCount { get; private set; }

    public DateTimeOffset? LastActivityAt { get; private set; }

    public static ProfileActivitySummary Create(Guid summaryId, RecommendationTargetType targetType) =>
        new(summaryId, targetType);

    public void Record(SignalKind kind, DateTimeOffset occurredAt)
    {
        switch (kind)
        {
            case SignalKind.Viewed:
            case SignalKind.NotificationOpened:
            case SignalKind.NotificationClicked:
            case SignalKind.Searched:
                ViewCount++;
                break;

            case SignalKind.Saved:
            case SignalKind.Followed:
            case SignalKind.Engaged:
                SaveCount++;
                break;

            case SignalKind.Applied:
            case SignalKind.Joined:
            case SignalKind.Participated:
            case SignalKind.LearningProgress:
            case SignalKind.LearningCompleted:
                ApplyCount++;
                break;

            case SignalKind.Transacted:
            case SignalKind.RecommendationAccepted:
                TransactCount++;
                break;

            case SignalKind.RecommendationDismissed:
                DismissCount++;
                break;

            default:
                // Location, language, trust, verification, onboarding, interest and preference
                // signals say something about the member rather than about a kind of thing. They
                // shape the profile elsewhere and are deliberately not counted as activity.
                return;
        }

        if (LastActivityAt is null || occurredAt > LastActivityAt)
        {
            LastActivityAt = occurredAt;
        }
    }

    /// <summary>Everything the member has done with this kind of thing, positive and negative.</summary>
    public int TotalCount => ViewCount + SaveCount + ApplyCount + TransactCount + DismissCount;

    /// <summary>
    /// How much of the member's engagement with this type was a refusal.
    /// <para>
    /// Consumed by the diversity strategy: a type a member reliably dismisses is demoted on the home
    /// feed rather than excluded, because a run of dismissals is evidence and not an instruction.
    /// </para>
    /// </summary>
    public decimal DismissalRate =>
        TotalCount == 0 ? 0m : decimal.Round((decimal)DismissCount / TotalCount, 4);

    public void Reset()
    {
        ViewCount = 0;
        SaveCount = 0;
        ApplyCount = 0;
        TransactCount = 0;
        DismissCount = 0;
        LastActivityAt = null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SummaryId;
        yield return TargetType;
        yield return ViewCount;
        yield return SaveCount;
        yield return ApplyCount;
        yield return TransactCount;
        yield return DismissCount;
    }
}

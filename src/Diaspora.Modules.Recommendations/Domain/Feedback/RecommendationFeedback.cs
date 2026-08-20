using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Feedback;

/// <summary>
/// What a member did with a recommendation.
/// <para>
/// <b>One row per interaction, not one row per target, and the difference is the whole quality
/// loop.</b> Collapsing to "the member's current opinion of this flat" would lose the sequence —
/// viewed on Monday, viewed again on Wednesday, dismissed on Friday — and the sequence is where the
/// information is. A dismissal after two views means something different from a dismissal on sight.
/// </para>
/// <para>
/// <b><see cref="SuppressedUntil"/> is on the dismissal row rather than in a separate suppression
/// table.</b> The suppression check runs on every generation for every member, so it must be one
/// indexed read; adding a second table would mean a join on the hottest query in the module to
/// answer a question this row already contains.
/// </para>
/// <para>
/// <b>Recorded even when the set is gone.</b> A member can dismiss a card whose generating set has
/// been swept, and refusing the dismissal because the paperwork expired would be the platform
/// ignoring a member's clearest possible instruction. <see cref="SetId"/>, <see cref="StrategyKey"/>
/// and <see cref="ReasonCode"/> are all optional for that reason.
/// </para>
/// </summary>
public sealed class RecommendationFeedback : AggregateRoot<RecommendationFeedbackId>, IAuditableEntity
{
    private RecommendationFeedback(
        RecommendationFeedbackId id,
        UserId userId,
        TargetReference target,
        RecommendationFeedbackKind kind,
        RecommendationSurface surface,
        DateTimeOffset occurredAt) : base(id)
    {
        UserId = userId;
        Target = target;
        Kind = kind;
        Surface = surface;
        OccurredAt = occurredAt;
    }

    private RecommendationFeedback() { }

    public UserId UserId { get; private set; } = default!;

    public TargetReference Target { get; private set; } = default!;

    public RecommendationFeedbackKind Kind { get; private set; }

    public RecommendationSurface Surface { get; private set; }

    /// <summary>The set that produced the card, when it is still known. See the class summary.</summary>
    public RecommendationSetId? SetId { get; private set; }

    public string? StrategyKey { get; private set; }

    public RecommendationReasonCode? ReasonCode { get; private set; }

    /// <summary>What the platform scored it at, frozen from the item.</summary>
    public decimal? Score { get; private set; }

    /// <summary>Where the card sat on the page. Position bias is real and this is how it is measured.</summary>
    public int? Position { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Dismissals only. Null on a dismissal means "never show this again".</summary>
    public DateTimeOffset? SuppressedUntil { get; private set; }

    /// <summary>When the analytics sweep folded this into a rollup. Bookkeeping, like the signal's.</summary>
    public DateTimeOffset? RolledUpAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RecommendationFeedback Record(
        RecommendationFeedbackId id,
        UserId userId,
        TargetReference target,
        RecommendationFeedbackKind kind,
        RecommendationSurface surface,
        DateTimeOffset occurredAt,
        RecommendationSetId? setId = null,
        string? strategyKey = null,
        RecommendationReasonCode? reasonCode = null,
        decimal? score = null,
        int? position = null,
        DateTimeOffset? suppressedUntil = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(target);

        var feedback = new RecommendationFeedback(id, userId, target, kind, surface, occurredAt)
        {
            SetId = setId,
            StrategyKey = string.IsNullOrWhiteSpace(strategyKey)
                ? null
                : strategyKey.Trim().ToLowerInvariant(),
            ReasonCode = reasonCode,
            Score = score,
            Position = position,
            SuppressedUntil = kind == RecommendationFeedbackKind.Dismissed ? suppressedUntil : null
        };

        switch (kind)
        {
            case RecommendationFeedbackKind.Dismissed:
                feedback.RaiseDomainEvent(new RecommendationDismissedDomainEvent
                {
                    FeedbackId = id,
                    UserId = userId,
                    TargetType = target.TargetType,
                    TargetId = target.TargetId,
                    Surface = surface,
                    StrategyKey = feedback.StrategyKey,
                    ReasonCode = reasonCode,
                    SuppressedUntil = feedback.SuppressedUntil,
                    DismissedAt = occurredAt
                });
                break;

            case RecommendationFeedbackKind.Accepted:
                feedback.RaiseDomainEvent(new RecommendationAcceptedDomainEvent
                {
                    FeedbackId = id,
                    UserId = userId,
                    TargetType = target.TargetType,
                    TargetId = target.TargetId,
                    Surface = surface,
                    StrategyKey = feedback.StrategyKey,
                    ReasonCode = reasonCode,
                    Score = score,
                    AcceptedAt = occurredAt
                });
                break;

            default:
                // A view, a click and a bookmark are counted, not announced. Publishing an event per
                // rendered card would put the platform's highest-volume fact on the bus for the
                // benefit of nobody — the rollup reads the rows directly.
                break;
        }

        return feedback;
    }

    /// <summary>
    /// Whether this dismissal still suppresses its target.
    /// <para>
    /// A null <see cref="SuppressedUntil"/> on a dismissal means forever, which is why the check
    /// cannot be written as a simple comparison and is written here once instead of at each call
    /// site.
    /// </para>
    /// </summary>
    public bool SuppressesAt(DateTimeOffset asOf) =>
        Kind == RecommendationFeedbackKind.Dismissed &&
        (SuppressedUntil is null || SuppressedUntil > asOf);

    public void MarkRolledUp(DateTimeOffset rolledUpAt) => RolledUpAt ??= rolledUpAt;

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

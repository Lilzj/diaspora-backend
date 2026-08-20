using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// The projection of Trust's decision about a guide.
/// <para>
/// Nothing on this type grants a badge. The aggregate moves it to <c>Pending</c> when a claim
/// goes to Trust, and every other transition is applied from a handler for a Trust integration
/// event — ADR-002's "Location owns the claim, Trust owns the proof", applied to knowledge.
/// </para>
/// <para>
/// <see cref="ChangedAt"/> carries the timestamp of the decision that produced this state, not
/// of the write that stored it. Delivery is at-least-once and unordered, so the aggregate
/// compares an incoming decision's timestamp against this one and drops the stale — without it,
/// a delayed redelivery of an old approval would silently re-verify a revoked guide.
/// </para>
/// </summary>
public sealed class GuideTrustMark : ValueObject
{
    private GuideTrustMark(
        GuideVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? changedAt)
    {
        State = state;
        VerificationId = verificationId;
        Badge = badge;
        Reason = reason;
        VerifiedAt = verifiedAt;
        ChangedAt = changedAt;
    }

    private GuideTrustMark() { }

    /// <summary>No claim has ever been made. What every guide starts with.</summary>
    public static GuideTrustMark None => new(GuideVerificationState.None, null, null, null, null, null);

    public GuideVerificationState State { get; private set; }

    /// <summary>Trust's own identifier for the claim, for matching decisions back.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>The badge Trust granted, when it granted one.</summary>
    public string? Badge { get; private set; }

    /// <summary>Why the mark is what it is. Shown to the author, never to readers.</summary>
    public string? Reason { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>When the decision that produced this state was made. The staleness guard.</summary>
    public DateTimeOffset? ChangedAt { get; private set; }

    /// <summary>A claim has gone to Trust and nothing has come back yet.</summary>
    public static GuideTrustMark Pending(DateTimeOffset now, Guid? verificationId = null) =>
        new(GuideVerificationState.Pending, verificationId, null, null, null, now);

    /// <summary>Trust said yes. Reachable only from a Trust integration event handler.</summary>
    public static GuideTrustMark Verified(
        Guid? verificationId, string? badge, DateTimeOffset verifiedAt) =>
        new(GuideVerificationState.Verified, verificationId, badge, null, verifiedAt, verifiedAt);

    /// <summary>Trust took it back. The reason is for the author.</summary>
    public static GuideTrustMark Revoked(
        Guid? verificationId, string? reason, DateTimeOffset revokedAt) =>
        new(GuideVerificationState.Revoked, verificationId, null, reason, null, revokedAt);

    /// <summary>Trust said no, or the claim lapsed. Back to nothing, with the reason kept.</summary>
    public static GuideTrustMark Cleared(string? reason, DateTimeOffset clearedAt) =>
        new(GuideVerificationState.None, null, null, reason, null, clearedAt);

    /// <summary>Whether a decision made at <paramref name="decidedAt"/> is older than this mark.</summary>
    public bool IsNewerThan(DateTimeOffset decidedAt) =>
        ChangedAt is { } changedAt && decidedAt <= changedAt;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return State;
        yield return VerificationId;
        yield return Badge;
        yield return Reason;
        yield return VerifiedAt;
        yield return ChangedAt;
    }
}

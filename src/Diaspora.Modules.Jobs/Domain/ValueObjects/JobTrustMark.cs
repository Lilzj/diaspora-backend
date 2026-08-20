using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// The projection of a Trust decision onto a posting or an employer.
/// <para>
/// <b>Nothing on this type grants a badge.</b> An aggregate moves it to
/// <see cref="JobVerificationState.Pending"/> when a claim goes to Trust through the published
/// <c>ITrustVerificationSubmission</c> contract, and every other transition is applied from a
/// handler for a Trust integration event. ADR-002's "the module owns the claim, Trust owns the
/// proof", applied to employment.
/// </para>
/// <para>
/// <b>This is the single most load-bearing badge in the module.</b> "Verified employer" is what
/// a candidate is actually reading a posting for on a platform whose members are being
/// approached by strangers offering work abroad — which is exactly the shape of the most common
/// fraud against this audience. It has to be Trust's word, and it has to be revocable.
/// </para>
/// <para>
/// <see cref="ChangedAt"/> carries the timestamp of the decision, not of the write. Delivery is
/// at-least-once and unordered, so an aggregate compares an incoming decision against this and
/// drops the stale one — without it, a delayed redelivery of an old approval silently
/// re-verifies an employer Trust has since revoked.
/// </para>
/// </summary>
public sealed class JobTrustMark : ValueObject
{
    private JobTrustMark(
        JobVerificationState state,
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

    private JobTrustMark() { }

    public JobVerificationState State { get; private set; }

    /// <summary>Trust's own case identifier, for matching a decision back to the claim.</summary>
    public Guid? VerificationId { get; private set; }

    /// <summary>The badge label Trust granted. Rendered, never interpreted.</summary>
    public string? Badge { get; private set; }

    /// <summary>Why the mark is what it is. Shown to the owner, never to candidates.</summary>
    public string? Reason { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>When the decision behind this state was made. The staleness guard.</summary>
    public DateTimeOffset? ChangedAt { get; private set; }

    /// <summary>No claim has ever been made. What everything starts with.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static JobTrustMark None =>
        new(JobVerificationState.None, null, null, null, null, null);

    /// <summary>A claim is with Trust and nothing has come back.</summary>
    public static JobTrustMark Pending(DateTimeOffset now, Guid? verificationId = null) =>
        new(JobVerificationState.Pending, verificationId, null, null, null, now);

    /// <summary>Trust said yes. Reachable only from a Trust integration event handler.</summary>
    public static JobTrustMark Verified(
        Guid? verificationId, string? badge, DateTimeOffset verifiedAt) =>
        new(JobVerificationState.Verified, verificationId, badge, null, verifiedAt, verifiedAt);

    /// <summary>Trust took back a mark it had granted.</summary>
    public static JobTrustMark Revoked(
        Guid? verificationId, string? reason, DateTimeOffset revokedAt) =>
        new(JobVerificationState.Revoked, verificationId, null, reason, null, revokedAt);

    /// <summary>Trust declined the claim. The reason is for the claimant.</summary>
    public static JobTrustMark Rejected(
        Guid? verificationId, string? reason, DateTimeOffset rejectedAt) =>
        new(JobVerificationState.Rejected, verificationId, null, reason, null, rejectedAt);

    /// <summary>The claim lapsed or was cleared. Back to nothing, with the reason kept.</summary>
    public static JobTrustMark Cleared(string? reason, DateTimeOffset clearedAt) =>
        new(JobVerificationState.None, null, null, reason, null, clearedAt);

    /// <summary>Whether the subject currently carries a granted mark.</summary>
    public bool IsVerified => State == JobVerificationState.Verified;

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

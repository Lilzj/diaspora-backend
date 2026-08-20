using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Trust;

/// <summary>
/// A verdict delivered to Identity by the Trust &amp; Verification Engine (Phase B3).
/// <para>
/// This type is the whole contract between the two subsystems. Identity accepts the
/// outcome and moves the access state; it does not know what evidence was weighed,
/// what residency rules applied, or how reputation was scored.
/// </para>
/// </summary>
public sealed class TrustDecision : ValueObject
{
    public const int MaxReasonLength = 512;

    public TrustDecisionOutcome Outcome { get; }
    public string? Reason { get; }

    /// <summary>Who or what issued the decision — a reviewer id, or an automated policy name.</summary>
    public string? DecidedBy { get; }

    public DateTimeOffset DecidedAt { get; }

    private TrustDecision(TrustDecisionOutcome outcome, string? reason, string? decidedBy, DateTimeOffset decidedAt)
    {
        Outcome = outcome;
        Reason = reason;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;
    }

    public static TrustDecision Create(
        TrustDecisionOutcome outcome,
        DateTimeOffset decidedAt,
        string? reason = null,
        string? decidedBy = null)
    {
        var trimmedReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim()[..Math.Min(reason.Trim().Length, MaxReasonLength)];

        return new TrustDecision(outcome, trimmedReason, decidedBy?.Trim(), decidedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Outcome;
        yield return Reason;
        yield return DecidedBy;
        yield return DecidedAt;
    }
}

namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// The inbound channel from the Trust &amp; Verification Engine (Phase B3).
/// <para>
/// This is the entire surface through which verification outcomes reach Identity.
/// Nothing inside the authentication subsystem may set a member to Verified by any
/// other route, which is what lets trust policy change without touching Identity.
/// </para>
/// </summary>
public sealed record ApplyTrustDecisionRequest
{
    public required Guid UserId { get; init; }

    /// <summary>One of UnderReview, Granted, Rejected, Revoked.</summary>
    public required string Outcome { get; init; }

    public string? Reason { get; init; }

    /// <summary>Reviewer identifier or the name of the automated policy that decided.</summary>
    public string? DecidedBy { get; init; }
}

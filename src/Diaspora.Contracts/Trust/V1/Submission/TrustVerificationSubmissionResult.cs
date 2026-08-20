namespace Diaspora.Contracts.Trust.V1.Submission;

/// <summary>
/// What Trust did with a submission.
/// <para>
/// <b>It never says "verified".</b> The richest thing this type can report is that a case is open
/// and who will decide it. An outcome arrives later, on
/// <c>VerificationApprovedIntegrationEvent</c> and its siblings, and a consumer that treats a
/// successful submission as a grant has misread both this contract and ADR-002.
/// </para>
/// </summary>
public sealed record TrustVerificationSubmissionResult
{
    /// <summary>
    /// Trust's identifier for the case, and the correlation key for every event about it.
    /// <para>
    /// Store it. Trust quotes it back on the approval, rejection, expiry and revocation events,
    /// and it is how an outcome finds the row that raised the claim — including after a replay,
    /// or for a consumer that never saw its own submission.
    /// </para>
    /// </summary>
    public required Guid VerificationId { get; init; }

    /// <summary>
    /// Trust's <c>VerificationStatus</c> name at the moment of submission — usually
    /// <c>Pending</c>, meaning queued for a human.
    /// <para>
    /// It can already be <c>Approved</c> or <c>Rejected</c> when the type's rule decided without
    /// a human: an expired document is a fact, not a judgement, and there is no reason to spend a
    /// reviewer on a foregone conclusion. A caller must not assume <c>Pending</c>.
    /// </para>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// True when this submission matched one Trust already held and no new workflow was opened.
    /// <para>
    /// <see cref="VerificationId"/> is the <em>original</em> case in that situation, which is what
    /// makes a retry safe. Not an error: a caller that retried after a timeout gets the answer it
    /// would have got the first time.
    /// </para>
    /// </summary>
    public required bool IsDuplicate { get; init; }

    /// <summary>When the case was opened — or, on a duplicate, when the original was.</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// When review is due, from the verification type's service level. Null when the type sets
    /// none, or when a rule already decided it.
    /// </summary>
    public DateTimeOffset? ReviewDueAt { get; init; }
}

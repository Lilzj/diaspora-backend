namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Moves an application through the hiring workflow.
/// <para>
/// The candidate's own exit — withdrawal — is deliberately not reachable through this shape. It
/// has its own route, because it is the candidate's decision and must not be issuable by an
/// employer passing an enum value.
/// </para>
/// </summary>
public sealed record DecideApplicationRequest
{
    /// <summary>
    /// <c>UnderReview</c>, <c>Shortlisted</c>, <c>InterviewScheduled</c>,
    /// <c>InterviewCompleted</c>, <c>OfferExtended</c>, <c>Accepted</c> or <c>Rejected</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why. On a rejection this is what the candidate is told, and a rejection with a reason is
    /// rare and worth far more to them than one without.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether the reason is shown to the candidate. False keeps it on the internal record only —
    /// which is the honest option when the real reason is not one to send.
    /// </summary>
    public bool ReasonVisibleToCandidate { get; init; } = true;
}

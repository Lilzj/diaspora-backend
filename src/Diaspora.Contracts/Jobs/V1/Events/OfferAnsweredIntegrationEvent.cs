using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// An offer reached an ending — accepted, rejected, withdrawn or expired.
/// <para>
/// One contract for all four, because the consumers that track the funnel respond to each the
/// same way: the offer is no longer outstanding, and the employer's acceptance rate moves.
/// <see cref="Status"/> separates them. An acceptance also publishes
/// <see cref="OfferAcceptedIntegrationEvent"/>, for consumers that only care about hires.
/// </para>
/// </summary>
public sealed record OfferAnsweredIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary><c>Accepted</c>, <c>Rejected</c>, <c>Withdrawn</c> or <c>Expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Null on an expiry, which nobody decided.</summary>
    public Guid? AnsweredBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset AnsweredAt { get; init; }

    /// <summary>
    /// The candidate's member identity — B13.1.
    /// <para>
    /// <b>Carried because two of the four endings are not the candidate's doing.</b> A
    /// withdrawal and an expiry both leave a member without the job they were deciding about,
    /// and neither of them presses a button — so both are messages the candidate has to receive,
    /// and until B13.1 there was no way to address them.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the application could not be read.</para>
    /// </summary>
    public Guid? CandidateUserId { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// The recipient for the two endings the employer did not cause: an acceptance, which is the
    /// news the whole pipeline exists for, and a rejection, which frees the vacancy.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}

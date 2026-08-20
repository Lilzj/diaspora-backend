using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A candidate withdrew their application.
/// <para>
/// <b>Its own contract rather than a rejection, because the difference matters to both
/// parties.</b> A withdrawal frees a slot in the employer's pipeline, must not count against
/// their response-time reputation, and says nothing about the candidate's quality. Products
/// that collapse it into "rejected" quietly penalise everyone involved.
/// </para>
/// <para>
/// <see cref="PreviousStatus"/> is how far they had got. Withdrawing the day after applying and
/// withdrawing after an offer are very different facts about a hiring process.
/// </para>
/// </summary>
public sealed record ApplicationWithdrawnIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>How far the candidate had got. <c>Shortlisted</c>, <c>OfferExtended</c>, and so on.</summary>
    public required string PreviousStatus { get; init; }

    /// <summary>The candidate's stated reason, when they gave one. Always optional.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset WithdrawnAt { get; init; }

    /// <summary>
    /// The candidate's member identity — B13.1.
    /// <para>
    /// The candidate withdrew, so nothing here is addressed to them. It is carried because a
    /// personalization consumer reading a withdrawal needs the member whose behaviour it
    /// describes, and deriving that from a profile identifier would mean a call into Jobs.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the application could not be read.</para>
    /// </summary>
    public Guid? CandidateUserId { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// <b>The recipient this contract exists for.</b> A withdrawal frees a slot in the
    /// employer's pipeline and is the one exit nobody at the employer caused, so it is the
    /// clearest employer-facing message in the module.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}

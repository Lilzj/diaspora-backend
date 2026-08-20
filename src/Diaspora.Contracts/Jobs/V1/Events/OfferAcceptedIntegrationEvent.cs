using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A candidate accepted an offer.
/// <para>
/// <b>The end of the funnel and the only fact in this module that means somebody got a job.</b>
/// It is what fills a vacancy, closes the posting when the last seat goes, updates the
/// employer's offer-acceptance rate, and completes the time-to-hire measurement.
/// </para>
/// <para>
/// Published separately from <see cref="OfferAnsweredIntegrationEvent"/>, which carries every
/// ending including this one, because a consumer that only cares about successful hires should
/// not have to subscribe to every rejection and expiry on the platform to find them.
/// </para>
/// </summary>
public sealed record OfferAcceptedIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    public DateOnly? StartDate { get; init; }

    /// <summary>How long the whole process took, from application to acceptance.</summary>
    public required TimeSpan TimeToHire { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }

    /// <summary>
    /// The candidate's member identity — B13.1.
    /// <para>
    /// The person who got the job. Carried so a personalization consumer can act on the single
    /// strongest positive outcome the platform produces without resolving a candidate profile
    /// through Jobs.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the application could not be read.</para>
    /// </summary>
    public Guid? CandidateUserId { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// <b>The recipient this contract exists for.</b> Somebody accepted, the employer did not do
    /// it, and it is the only fact in the module that means a vacancy was filled.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}

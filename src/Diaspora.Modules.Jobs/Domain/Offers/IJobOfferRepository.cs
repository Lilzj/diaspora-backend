using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Offers;

/// <summary>Reads and writes offers.</summary>
public interface IJobOfferRepository
{
    Task<JobOffer?> GetAsync(JobOfferId id, CancellationToken cancellationToken = default);

    Task<JobOffer?> GetForReadAsync(JobOfferId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The offer currently with a candidate on this application, if any. Tracked.
    /// <para>
    /// What "an offer is already out with this candidate" is answered by, and what the
    /// candidate's own screen loads. There can be at most one live at a time; superseded ones
    /// are reachable through <see cref="GetForApplicationAsync"/>.
    /// </para>
    /// </summary>
    Task<JobOffer?> GetLiveForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default);

    /// <summary>Every offer ever made on this application, newest first. The revision trail.</summary>
    Task<IReadOnlyList<JobOffer>> GetForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default);

    /// <summary>An employer's outstanding offers. The dashboard tile and the chase list.</summary>
    Task<IReadOnlyList<JobOffer>> GetOutstandingForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default);

    /// <summary>A candidate's outstanding offers. Their dashboard's most urgent card.</summary>
    Task<IReadOnlyList<JobOffer>> GetOutstandingForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offers whose deadline has passed but whose status has not caught up, tracked and
    /// bounded. The expiry sweep's work list — it writes the fact down and notifies; the fact
    /// itself is already true, because the aggregate computes expiry from the clock.
    /// </summary>
    Task<IReadOnlyList<JobOffer>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(JobOffer offer, CancellationToken cancellationToken = default);

    void Remove(JobOffer offer);
}

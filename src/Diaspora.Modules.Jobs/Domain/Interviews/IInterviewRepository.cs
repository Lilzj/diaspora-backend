using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Interviews;

/// <summary>Reads and writes interviews.</summary>
public interface IInterviewRepository
{
    Task<Interview?> GetAsync(InterviewId id, CancellationToken cancellationToken = default);

    Task<Interview?> GetForReadAsync(
        InterviewId id, CancellationToken cancellationToken = default);

    /// <summary>Every round against one application, oldest first.</summary>
    Task<IReadOnlyList<Interview>> GetForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The highest round number recorded against an application. What the next round's number
    /// is derived from, asked as a scalar rather than by loading every previous round.
    /// </summary>
    Task<int> GetHighestRoundAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// An employer's upcoming interviews, soonest first. The recruiter's calendar, and the
    /// source of the reminder sweep.
    /// </summary>
    Task<IReadOnlyList<Interview>> GetUpcomingForEmployerAsync(
        EmployerProfileId employerId,
        DateTimeOffset from,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>A candidate's upcoming interviews, soonest first. Their dashboard's top card.</summary>
    Task<IReadOnlyList<Interview>> GetUpcomingForCandidateAsync(
        CandidateProfileId candidateId,
        DateTimeOffset from,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Interviews that have happened but whose outcome nobody recorded. The nudge sweep's work
    /// list — an unrecorded outcome is a candidate left waiting with no way to ask.
    /// </summary>
    Task<IReadOnlyList<Interview>> GetAwaitingOutcomeAsync(
        DateTimeOffset endedBefore, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(Interview interview, CancellationToken cancellationToken = default);

    void Remove(Interview interview);
}

using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Domain.Saved;

/// <summary>Reads and writes saved jobs.</summary>
public interface ISavedJobRepository
{
    Task<SavedJob?> GetAsync(SavedJobId id, CancellationToken cancellationToken = default);

    /// <summary>This candidate's save of this posting, if any. What makes saving idempotent.</summary>
    Task<SavedJob?> GetByCandidateAndPostingAsync(
        CandidateProfileId candidateId,
        JobPostingId postingId,
        CancellationToken cancellationToken = default);

    /// <summary>The candidate's saved list, newest first.</summary>
    Task<CursorPagedResult<SavedJob>> GetForCandidateAsync(
        CandidateProfileId candidateId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many the candidate has saved. Enforces the ceiling.</summary>
    Task<int> CountForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these postings this member has saved. Lets a results page render the filled
    /// bookmark on twenty cards without twenty queries.
    /// </summary>
    Task<IReadOnlySet<Guid>> FindSavedPostingIdsAsync(
        UserId userId,
        IReadOnlyCollection<JobPostingId> postingIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves whose posting closes soon and whose reminder has not gone out, tracked and
    /// bounded. The reminder sweep's work list.
    /// </summary>
    Task<IReadOnlyList<SavedJob>> GetDueRemindersAsync(
        DateTimeOffset closingBefore, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(SavedJob savedJob, CancellationToken cancellationToken = default);

    void Remove(SavedJob savedJob);
}

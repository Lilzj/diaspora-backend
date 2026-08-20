using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Resumes;

/// <summary>Reads and writes résumé versions.</summary>
public interface IResumeRepository
{
    Task<Resume?> GetAsync(ResumeId id, CancellationToken cancellationToken = default);

    /// <summary>Every version this candidate holds, newest first. Tracked, so the default
    /// flag can be moved across them in one unit of work.</summary>
    Task<IReadOnlyList<Resume>> GetForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default);

    /// <summary>The version attached when the candidate does not choose one.</summary>
    Task<Resume?> GetDefaultAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several versions at once, untracked. An applicant list shows which résumé each
    /// application carried, and doing that one row at a time is a page of round trips.
    /// </summary>
    Task<IReadOnlyList<Resume>> GetManyAsync(
        IReadOnlyCollection<ResumeId> ids, CancellationToken cancellationToken = default);

    /// <summary>How many versions this candidate holds. Enforces the per-candidate ceiling.</summary>
    Task<int> CountForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A version by identity and owner, tracked. The ownership-checked read every résumé
    /// command starts with, so "not yours" and "does not exist" are one query rather than two.
    /// </summary>
    Task<Resume?> GetOwnedAsync(
        ResumeId id, UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(Resume resume, CancellationToken cancellationToken = default);

    void Remove(Resume resume);
}

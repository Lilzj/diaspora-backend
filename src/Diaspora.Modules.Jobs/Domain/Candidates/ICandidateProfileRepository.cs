using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// Reads and writes candidate profiles.
/// <para>
/// <see cref="GetByUserAsync"/> is the one nearly every handler calls: a candidate command
/// arrives with a member identity, not a profile identity, and resolving one to the other in a
/// single place is what keeps "you need a profile first" a single error rather than a check
/// each handler remembers differently.
/// </para>
/// <para>
/// <b>There is deliberately no unrestricted listing.</b> Every search of candidates goes through
/// <see cref="SearchAsync"/>, which applies the visibility rules — because a repository method
/// that returned candidates without them would eventually be called by something that did not
/// know it needed to.
/// </para>
/// </summary>
public interface ICandidateProfileRepository
{
    /// <summary>The full aggregate, tracked. For commands.</summary>
    Task<CandidateProfile?> GetAsync(
        CandidateProfileId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the profile screen.</summary>
    Task<CandidateProfile?> GetForReadAsync(
        CandidateProfileId id, CancellationToken cancellationToken = default);

    /// <summary>This member's profile, tracked. How a candidate command resolves its subject.</summary>
    Task<CandidateProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>This member's profile, untracked.</summary>
    Task<CandidateProfile?> GetByUserForReadAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Several profiles at once, untracked. Hydrates an applicant list.</summary>
    Task<IReadOnlyList<CandidateProfile>> GetManyAsync(
        IReadOnlyCollection<CandidateProfileId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Candidate search, with the visibility rules applied inside.
    /// <para>
    /// The filter carries the searching employer so a candidate who set themselves visible to
    /// verified employers only is matched against that employer's actual trust mark rather than
    /// against a caller's assertion.
    /// </para>
    /// </summary>
    Task<CursorPagedResult<CandidateProfile>> SearchAsync(
        CandidateSearchFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this member already has a profile. Enforces one-per-member.</summary>
    Task<bool> ExistsForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(CandidateProfile profile, CancellationToken cancellationToken = default);

    void Remove(CandidateProfile profile);
}

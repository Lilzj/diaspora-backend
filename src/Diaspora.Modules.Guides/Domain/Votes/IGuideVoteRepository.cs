using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Votes;

/// <summary>Reads and writes helpfulness votes.</summary>
public interface IGuideVoteRepository
{
    Task<GuideVote?> GetAsync(GuideVoteId id, CancellationToken cancellationToken = default);

    /// <summary>A member's existing vote on a guide, if any. Enforces one vote each.</summary>
    Task<GuideVote?> GetByGuideAndVoterAsync(
        GuideId guideId, UserId voterId, CancellationToken cancellationToken = default);

    Task AddAsync(GuideVote vote, CancellationToken cancellationToken = default);

    void Remove(GuideVote vote);
}

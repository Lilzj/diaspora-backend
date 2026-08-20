using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Domain.Authors;

/// <summary>
/// Reads and writes author standing projections. Get-or-create per user — the unique user
/// index arbitrates a race between two events about the same author.
/// </summary>
public interface IGuideAuthorStandingRepository
{
    Task<GuideAuthorStanding?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Several authors' standings at once. The N+1 guard for a discovery page.</summary>
    Task<IReadOnlyList<GuideAuthorStanding>> GetManyAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);

    Task AddAsync(GuideAuthorStanding standing, CancellationToken cancellationToken = default);
}

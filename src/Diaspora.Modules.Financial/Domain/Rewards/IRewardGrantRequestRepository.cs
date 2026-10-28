using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>Staff grants waiting for, or decided by, a second Admin (RWD-10).</summary>
public interface IRewardGrantRequestRepository
{
    void Add(RewardGrantRequest request);

    void Update(RewardGrantRequest request);

    Task<RewardGrantRequest?> FindAsync(
        RewardGrantRequestId requestId, CancellationToken cancellationToken = default);

    /// <summary>The request carrying this grant reference, staged or saved. A retry replays it.</summary>
    Task<RewardGrantRequest?> FindByReferenceAsync(
        string grantReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests newest first, paged like every other read here: <paramref name="before"/> is
    /// inclusive and <paramref name="excludeIds"/> are the ones already shown at that instant.
    /// Null <paramref name="status"/> lists every request.
    /// </summary>
    Task<IReadOnlyList<RewardGrantRequest>> ListAsync(
        RewardGrantRequestStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);
}

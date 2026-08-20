using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>Retrieval and staging for reward accounts.</summary>
public interface IRewardAccountRepository
{
    void Add(RewardAccount account);

    void Update(RewardAccount account);

    Task<RewardAccount?> FindAsync(
        RewardAccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A member's account, staged or saved.
    /// <para>
    /// Consults the change tracker first: a grant that opens an account and immediately awards
    /// against it does both in one unit of work, and a lookup that only asked the database would open
    /// a second one for the unique index to refuse at save time.
    /// </para>
    /// </summary>
    Task<RewardAccount?> FindByMemberAsync(
        Guid memberUserId, CancellationToken cancellationToken = default);
}

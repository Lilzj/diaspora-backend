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
    /// Accounts holding a pending grant due to vest by <paramref name="asOf"/>, oldest due first
    /// (D-9). Held accounts are included; the sweep decides what a hold means.
    /// </summary>
    Task<IReadOnlyList<RewardAccountId>> ListDueForVestingAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// A member's balance and whether conversion is held: all the conversion terms need, read
    /// without loading the entry history the aggregate carries. Null when the member has never
    /// earned a point.
    /// </summary>
    Task<RewardAccountStanding?> FindStandingAsync(
        Guid memberUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many points one staff member has awarded by hand since a moment, across every account.
    /// What the <c>reward.grant</c> policy's daily ceiling is checked against.
    /// </summary>
    Task<long> PointsGrantedBySinceAsync(
        Guid grantorUserId, DateTimeOffset since, CancellationToken cancellationToken = default);
}

/// <summary>The two facts about a points account that decide whether it may convert.</summary>
public readonly record struct RewardAccountStanding(long Balance, bool IsConversionBlocked);

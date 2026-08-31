using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Withdrawals;

/// <summary>Retrieval and staging for payout requests.</summary>
public interface IWithdrawalRepository
{
    void Add(Withdrawal withdrawal);

    void Update(Withdrawal withdrawal);

    Task<Withdrawal?> FindAsync(
        WithdrawalId withdrawalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The idempotency check, scoped to the requester.
    /// <para>
    /// <paramref name="requestedByUserId"/> is half the key rather than a filter, and has no
    /// default. Matching on the key alone answered one member's request with another
    /// member's withdrawal and created nothing for the caller.
    /// </para>
    /// </summary>
    Task<Withdrawal?> FindByIdempotencyKeyAsync(
        Guid requestedByUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Withdrawal>> ListForUserAsync(
        Guid userId,
        WithdrawalStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What a member has already taken out today, in one currency.
    /// <para>
    /// Counts everything that is not refused: a request still held is money already committed, and a
    /// daily cap that only counted completed payouts would let somebody raise a hundred requests
    /// before the first one settled.
    /// </para>
    /// </summary>
    Task<long> SumSinceAsync(
        Guid userId,
        string currency,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Requests waiting for a settlement provider to pick them up, oldest first.</summary>
    Task<IReadOnlyList<Withdrawal>> ListPendingAsync(
        int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests handed to a provider whose outcome is still unknown, oldest first.
    /// <para>
    /// <paramref name="startedBefore"/> keeps the resolution pass off instructions that were
    /// submitted moments ago and are simply still in flight; asking a provider about a transfer it
    /// is currently creating produces a "not found" that means nothing.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Withdrawal>> ListUnresolvedAsync(
        DateTimeOffset startedBefore, int take, CancellationToken cancellationToken = default);
}

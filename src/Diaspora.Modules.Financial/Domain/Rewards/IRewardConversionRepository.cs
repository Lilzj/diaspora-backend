using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>Retrieval and staging for reward conversions.</summary>
public interface IRewardConversionRepository
{
    void Add(RewardConversion conversion);

    void Update(RewardConversion conversion);

    Task<RewardConversion?> FindAsync(
        RewardConversionId conversionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The idempotency check, scoped to the member.
    /// <para>
    /// <paramref name="memberUserId"/> is half the key rather than a filter, and has no
    /// default. Matching on the key alone answered one member's conversion request with
    /// another member's conversion and converted nothing for the caller.
    /// </para>
    /// </summary>
    Task<RewardConversion?> FindByIdempotencyKeyAsync(
        Guid memberUserId,
        string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RewardConversion>> ListForMemberAsync(
        Guid memberUserId,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Points converted by a member since a moment. The daily-cap input.</summary>
    Task<long> PointsConvertedSinceAsync(
        Guid memberUserId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Conversions whose hold has ended and whose credit has not been released, oldest first.</summary>
    Task<IReadOnlyList<RewardConversion>> ListDueForReleaseAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}

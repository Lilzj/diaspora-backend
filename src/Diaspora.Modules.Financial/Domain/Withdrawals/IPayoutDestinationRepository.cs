using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Withdrawals;

/// <summary>Retrieval and staging for payout destinations.</summary>
public interface IPayoutDestinationRepository
{
    void Add(PayoutDestination destination);

    void Update(PayoutDestination destination);

    Task<PayoutDestination?> FindAsync(
        PayoutDestinationId destinationId, CancellationToken cancellationToken = default);

    /// <summary>A member's destinations. Removed ones are excluded unless asked for.</summary>
    Task<IReadOnlyList<PayoutDestination>> ListForUserAsync(
        Guid userId, bool includeRemoved, CancellationToken cancellationToken = default);
}

using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Credits;

/// <summary>Retrieval and staging for credit grants.</summary>
public interface ICreditGrantRepository
{
    void Add(CreditGrant grant);

    void Update(CreditGrant grant);

    Task<CreditGrant?> FindAsync(CreditGrantId grantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditGrant>> ListForWalletAsync(
        WalletId walletId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditGrant>> ListForOwnerAsync(
        Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Live grants past their expiry. The sweep's input.</summary>
    Task<IReadOnlyList<CreditGrant>> ListExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}

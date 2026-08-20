using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Wallets;

/// <summary>Retrieval and staging for wallets.</summary>
public interface IWalletRepository
{
    void Add(Wallet wallet);

    void Update(Wallet wallet);

    Task<Wallet?> FindAsync(WalletId walletId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The one wallet of a kind and currency on an account.
    /// <para>
    /// The uniqueness is enforced by a database index as well, because two primary GBP wallets on
    /// one account is the kind of duplicate that produces two half-right balances and no error.
    /// </para>
    /// </summary>
    Task<Wallet?> FindByAccountAsync(
        FinancialAccountId accountId,
        WalletType walletType,
        string currency,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Wallet>> ListByAccountAsync(
        FinancialAccountId accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Wallet>> ListByOwnerAsync(
        Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>The platform's wallet of a kind and currency. The counterparty to everything.</summary>
    Task<Wallet?> FindPlatformWalletAsync(
        WalletType walletType, string currency, CancellationToken cancellationToken = default);
}

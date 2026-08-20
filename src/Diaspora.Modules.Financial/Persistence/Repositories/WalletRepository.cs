using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for wallets.</summary>
public sealed class WalletRepository : IWalletRepository
{
    private readonly DiasporaDbContext _dbContext;

    public WalletRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Wallet> Wallets => _dbContext.Set<Wallet>();

    public void Add(Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        Wallets.Add(wallet);
    }

    public void Update(Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        FinancialAggregateUpdater.MarkDirty(_dbContext, wallet);
    }

    public async Task<Wallet?> FindAsync(
        WalletId walletId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(walletId);

        return await Wallets.FirstOrDefaultAsync(wallet => wallet.Id == walletId, cancellationToken);
    }

    /// <summary>
    /// The one wallet of a kind and currency on an account, staged or saved.
    /// <para>
    /// <b>The change tracker is consulted first, and that is not an optimisation.</b> Provisioning
    /// stages wallets without saving — a first payment opens an identity and an intent in one
    /// transaction — so a lookup that only asked the database would find nothing and open a second
    /// set, which the unique index then refuses at save time with an error naming three columns and
    /// nothing about why. Two primary GBP wallets on one account is the duplicate that produces two
    /// half-right balances derived from one ledger.
    /// </para>
    /// </summary>
    public async Task<Wallet?> FindByAccountAsync(
        FinancialAccountId accountId,
        WalletType walletType,
        string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        var staged = Wallets.Local.FirstOrDefault(
            wallet => wallet.AccountId == accountId
                && wallet.WalletType == walletType
                && string.Equals(wallet.Currency, normalized, StringComparison.Ordinal));

        if (staged is not null)
        {
            return staged;
        }

        return await Wallets.FirstOrDefaultAsync(
            wallet => wallet.AccountId == accountId
                && wallet.WalletType == walletType
                && wallet.Currency == normalized,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Wallet>> ListByAccountAsync(
        FinancialAccountId accountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        return await Wallets
            .Where(wallet => wallet.AccountId == accountId)
            .OrderBy(wallet => wallet.WalletType)
            .ThenBy(wallet => wallet.Currency)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Wallet>> ListByOwnerAsync(
        Guid ownerId, CancellationToken cancellationToken = default) =>
        await Wallets
            .Where(wallet => wallet.OwnerId == ownerId)
            .OrderBy(wallet => wallet.WalletType)
            .ThenBy(wallet => wallet.Currency)
            .ToListAsync(cancellationToken);

    public async Task<Wallet?> FindPlatformWalletAsync(
        WalletType walletType, string currency, CancellationToken cancellationToken = default)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        // Staged first, for the reason FindByAccountAsync records: the seeder opens the platform's
        // wallets and posts against them inside one unit of work.
        var staged = Wallets.Local.FirstOrDefault(
            wallet => wallet.OwnerType == FinancialAccountType.Platform
                && wallet.WalletType == walletType
                && string.Equals(wallet.Currency, normalized, StringComparison.Ordinal));

        if (staged is not null)
        {
            return staged;
        }

        return await Wallets.FirstOrDefaultAsync(
            wallet => wallet.OwnerType == FinancialAccountType.Platform
                && wallet.WalletType == walletType
                && wallet.Currency == normalized,
            cancellationToken);
    }
}

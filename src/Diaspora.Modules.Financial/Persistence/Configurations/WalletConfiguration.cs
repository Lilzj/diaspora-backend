using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the wallet projection.
/// <para>
/// <b>Every balance is a 64-bit integer count of minor units, and no column here is a decimal.</b>
/// A financial platform that stored a display format and reconciled it later would be storing the
/// wrong thing.
/// </para>
/// <para>
/// The unique index on account, type and currency is what makes "one primary GBP wallet per account"
/// a database guarantee. Two would produce two half-right balances derived from one ledger, with
/// nothing to say which was right.
/// </para>
/// </summary>
public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.WalletsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.Id)
            .HasConversion(FinancialValueConverters.Id<WalletId>());

        builder.Property(wallet => wallet.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(wallet => wallet.Revision).IsConcurrencyToken();

        builder.Property(wallet => wallet.OwnerType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(wallet => wallet.WalletType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(wallet => wallet.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(wallet => wallet.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(wallet => wallet.StatusReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(wallet => new { wallet.AccountId, wallet.WalletType, wallet.Currency })
            .IsUnique()
            .HasDatabaseName("ix_financial_wallets_account_type_currency");

        builder.HasIndex(wallet => wallet.OwnerId)
            .HasDatabaseName("ix_financial_wallets_owner");

        builder.Ignore(wallet => wallet.DomainEvents);

        // Derived from the stored columns; nothing to map.
        builder.Ignore(wallet => wallet.Available);
        builder.Ignore(wallet => wallet.Pending);
        builder.Ignore(wallet => wallet.Reserved);
        builder.Ignore(wallet => wallet.LifetimeCredits);
        builder.Ignore(wallet => wallet.LifetimeDebits);
        builder.Ignore(wallet => wallet.NextSequence);
        builder.Ignore(wallet => wallet.AllowsNegativeBalance);
    }
}

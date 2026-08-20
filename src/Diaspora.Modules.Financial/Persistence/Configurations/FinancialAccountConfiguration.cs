using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps financial identities.
/// <para>
/// The unique index on owner and type is what makes "one financial identity per owner" enforceable
/// at the database rather than only in a handler — two accounts for one member is the kind of
/// duplicate that produces two half-right balances and no error anywhere.
/// </para>
/// </summary>
public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.AccountsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>());

        builder.Property(account => account.Revision).IsConcurrencyToken();

        builder.Property(account => account.AccountType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(account => account.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(account => account.PrimaryCurrency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(account => account.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(account => account.DisplayName)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(account => account.RestrictionReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(account => new { account.OwnerId, account.AccountType })
            .IsUnique()
            .HasDatabaseName("ix_financial_accounts_owner");

        builder.HasIndex(account => account.IsPlatformAccount)
            .HasDatabaseName("ix_financial_accounts_platform");

        builder.Ignore(account => account.DomainEvents);
    }
}

using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps payout requests.
/// <para>
/// <b>The unique index on the idempotency key is what makes a double-tapped withdraw button one
/// withdrawal.</b> A handler check would be a race between two workers; this is the database
/// refusing.
/// </para>
/// </summary>
public sealed class WithdrawalConfiguration : IEntityTypeConfiguration<Withdrawal>
{
    public void Configure(EntityTypeBuilder<Withdrawal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.WithdrawalsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(withdrawal => withdrawal.Id);

        builder.Property(withdrawal => withdrawal.Id)
            .HasConversion(FinancialValueConverters.Id<WithdrawalId>());

        builder.Property(withdrawal => withdrawal.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(withdrawal => withdrawal.WalletId)
            .HasConversion(FinancialValueConverters.Id<WalletId>())
            .IsRequired();

        builder.Property(withdrawal => withdrawal.DestinationId)
            .HasConversion(FinancialValueConverters.Id<PayoutDestinationId>())
            .IsRequired();

        builder.Property(withdrawal => withdrawal.Revision).IsConcurrencyToken();

        builder.Property(withdrawal => withdrawal.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(withdrawal => withdrawal.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(withdrawal => withdrawal.DestinationLabel)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(withdrawal => withdrawal.SettlementProvider)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(withdrawal => withdrawal.SettlementReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(withdrawal => withdrawal.FailureCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(withdrawal => withdrawal.FailureMessage)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.Property(withdrawal => withdrawal.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(withdrawal => withdrawal.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.HasIndex(withdrawal => withdrawal.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_financial_withdrawals_idempotency");

        builder.HasIndex(withdrawal => new { withdrawal.RequestedByUserId, withdrawal.RequestedAt })
            .HasDatabaseName("ix_financial_withdrawals_requester");

        builder.HasIndex(withdrawal => new { withdrawal.Status, withdrawal.RequestedAt })
            .HasDatabaseName("ix_financial_withdrawals_status");

        builder.Ignore(withdrawal => withdrawal.DomainEvents);
        builder.Ignore(withdrawal => withdrawal.Gross);
        builder.Ignore(withdrawal => withdrawal.Fee);
        builder.Ignore(withdrawal => withdrawal.Net);
        builder.Ignore(withdrawal => withdrawal.IsHeld);
        builder.Ignore(withdrawal => withdrawal.IsTerminal);
    }
}

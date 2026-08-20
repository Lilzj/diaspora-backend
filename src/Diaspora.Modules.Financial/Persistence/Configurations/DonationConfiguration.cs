using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps donations.
/// <para>
/// <b>The unique index on the receipt reference is not a nicety.</b> An anonymous donation stores no
/// donor identifier at all, so the receipt is the only key that finds it — for the donor, for
/// support, and for a refund. A duplicate would make one of them unreachable.
/// </para>
/// </summary>
public sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.DonationsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(donation => donation.Id);

        builder.Property(donation => donation.Id)
            .HasConversion(FinancialValueConverters.Id<DonationId>());

        builder.Property(donation => donation.CauseId)
            .HasConversion(FinancialValueConverters.Id<DonationCauseId>());

        builder.Property(donation => donation.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(donation => donation.TransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(donation => donation.Revision).IsConcurrencyToken();

        builder.Property(donation => donation.BeneficiaryType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(donation => donation.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(donation => donation.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(donation => donation.CauseCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(donation => donation.Message)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength);

        builder.Property(donation => donation.ReceiptReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength)
            .IsRequired();

        builder.Property(donation => donation.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(donation => donation.FailureReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(donation => donation.ReceiptReference)
            .IsUnique()
            .HasDatabaseName("ix_financial_donations_receipt");

        builder.HasIndex(donation => new { donation.DonorUserId, donation.CreatedAt })
            .HasDatabaseName("ix_financial_donations_donor");

        builder.HasIndex(donation => new { donation.CauseId, donation.Status })
            .HasDatabaseName("ix_financial_donations_cause");

        builder.Ignore(donation => donation.DomainEvents);
        builder.Ignore(donation => donation.Amount);
    }
}

using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps community contributions.
/// <para>
/// The pledge is three nullable columns rather than an optional owned type, because a monetary
/// contribution genuinely has no pledge and EF cannot tell an absent optional dependent from one
/// whose properties are all null.
/// </para>
/// </summary>
public sealed class CommunityContributionConfiguration
    : IEntityTypeConfiguration<CommunityContribution>
{
    public void Configure(EntityTypeBuilder<CommunityContribution> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.ContributionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(contribution => contribution.Id);

        builder.Property(contribution => contribution.Id)
            .HasConversion(FinancialValueConverters.Id<CommunityContributionId>());

        builder.Property(contribution => contribution.FundId)
            .HasConversion(FinancialValueConverters.Id<ContributionFundId>())
            .IsRequired();

        builder.Property(contribution => contribution.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(contribution => contribution.TransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(contribution => contribution.Revision).IsConcurrencyToken();

        builder.Property(contribution => contribution.FundCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(contribution => contribution.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(contribution => contribution.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(contribution => contribution.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength);

        builder.Property(contribution => contribution.PledgeQuantity)
            .HasPrecision(
                FinancialPersistenceConstants.QuantityPrecision,
                FinancialPersistenceConstants.QuantityScale);

        builder.Property(contribution => contribution.PledgeUnit)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(contribution => contribution.PledgeDescription)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength);

        builder.Property(contribution => contribution.Message)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength);

        builder.Property(contribution => contribution.ReceiptReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength)
            .IsRequired();

        builder.Property(contribution => contribution.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(contribution => contribution.FailureReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(contribution => contribution.ReceiptReference)
            .IsUnique()
            .HasDatabaseName("ix_financial_contributions_receipt");

        builder.HasIndex(contribution => new { contribution.FundId, contribution.Status })
            .HasDatabaseName("ix_financial_contributions_fund");

        builder.HasIndex(contribution =>
                new { contribution.ContributorUserId, contribution.CreatedAt })
            .HasDatabaseName("ix_financial_contributions_contributor");

        builder.Ignore(contribution => contribution.DomainEvents);
        builder.Ignore(contribution => contribution.Amount);
        builder.Ignore(contribution => contribution.Pledge);
        builder.Ignore(contribution => contribution.IsMonetary);
    }
}

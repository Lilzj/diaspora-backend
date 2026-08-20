using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps community funds.</summary>
public sealed class ContributionFundConfiguration : IEntityTypeConfiguration<ContributionFund>
{
    public void Configure(EntityTypeBuilder<ContributionFund> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.FundsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(fund => fund.Id);

        builder.Property(fund => fund.Id)
            .HasConversion(FinancialValueConverters.Id<ContributionFundId>());

        builder.Property(fund => fund.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>());

        builder.Property(fund => fund.Revision).IsConcurrencyToken();

        builder.Property(fund => fund.Code)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(fund => fund.Name)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(fund => fund.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(fund => fund.Urgency)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(fund => fund.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(fund => fund.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(fund => fund.AcceptedKinds)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(fund => fund.MediaReference)
            .HasMaxLength(FinancialPersistenceConstants.MediaReferenceLength);

        builder.HasIndex(fund => fund.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_funds_code");

        builder.HasIndex(fund => new { fund.IsActive, fund.CountryCode, fund.SortOrder })
            .HasDatabaseName("ix_financial_funds_active");

        builder.Ignore(fund => fund.DomainEvents);
        builder.Ignore(fund => fund.Raised);
        builder.Ignore(fund => fund.Target);
    }
}

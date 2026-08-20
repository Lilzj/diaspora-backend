using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps the currency catalogue.</summary>
public sealed class CurrencyDefinitionConfiguration : IEntityTypeConfiguration<CurrencyDefinition>
{
    public void Configure(EntityTypeBuilder<CurrencyDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.CurrenciesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(currency => currency.Id);

        builder.Property(currency => currency.Id)
            .HasConversion(FinancialValueConverters.Id<CurrencyDefinitionId>());

        builder.Property(currency => currency.Revision).IsConcurrencyToken();

        builder.Property(currency => currency.Code)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(currency => currency.Name)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(currency => currency.Symbol)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength)
            .IsRequired();

        builder.HasIndex(currency => currency.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_currencies_code");

        builder.Ignore(currency => currency.DomainEvents);
    }
}

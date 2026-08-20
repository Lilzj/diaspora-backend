using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Revenue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps revenue rollups.
/// <para>
/// The unique index on period, dimension, value and currency is what makes a re-roll update rather
/// than duplicate — a late refund arriving after a period closed has to correct the row, not add a
/// second one beside it.
/// </para>
/// </summary>
public sealed class RevenueRecordConfiguration : IEntityTypeConfiguration<RevenueRecord>
{
    public void Configure(EntityTypeBuilder<RevenueRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.RevenueTable, FinancialModuleMetadata.Schema);

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasConversion(FinancialValueConverters.Id<RevenueRecordId>());

        builder.Property(record => record.Revision).IsConcurrencyToken();

        builder.Property(record => record.Dimension)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(record => record.DimensionValue)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(record => record.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.HasIndex(record => new
        {
            record.PeriodStart,
            record.Dimension,
            record.DimensionValue,
            record.Currency
        })
            .IsUnique()
            .HasDatabaseName("ix_financial_revenue_period");

        builder.Ignore(record => record.DomainEvents);
        builder.Ignore(record => record.Gross);
        builder.Ignore(record => record.Net);
        builder.Ignore(record => record.Commission);
        builder.Ignore(record => record.Fees);
        builder.Ignore(record => record.Refunds);
    }
}

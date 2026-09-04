using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps delivery counters.
/// <para>
/// <b>The unique index is the recorder&apos;s correctness, not an optimisation.</b> Two rows for the
/// same campaign, placement, surface and day would both be found by nothing and incremented by
/// whichever the lookup happened to return — so a campaign&apos;s impressions would drift downward
/// with no error anywhere. The index makes the second insert a failure instead.
/// </para>
/// <para>
/// <b>No viewer column, deliberately.</b> These rows record how many, never who. See
/// <see cref="AdPerformanceDay"/> for why that is a privacy decision before it is a storage one.
/// </para>
/// </summary>
public sealed class AdPerformanceDayConfiguration : IEntityTypeConfiguration<AdPerformanceDay>
{
    public void Configure(EntityTypeBuilder<AdPerformanceDay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.AdPerformanceTable, FinancialModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(FinancialValueConverters.Id<AdPerformanceDayId>())
            .ValueGeneratedNever();

        builder.Property(row => row.CampaignId)
            .HasConversion(FinancialValueConverters.Id<AdCampaignId>())
            .IsRequired();

        builder.Property(row => row.PlacementId)
            .HasConversion(FinancialValueConverters.Id<PlacementId>());

        builder.Property(row => row.Surface)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(row => row.Day).IsRequired();

        builder.Property(row => row.Impressions).IsRequired();
        builder.Property(row => row.Clicks).IsRequired();
        builder.Property(row => row.Conversions).IsRequired();
        builder.Property(row => row.SpendMinorUnits).IsRequired();

        builder.Property(row => row.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(row => row.Exponent).IsRequired();

        // The recorder's lookup, and the constraint that keeps the counters single-valued.
        builder.HasIndex(row => new
            {
                row.CampaignId,
                row.PlacementId,
                row.Surface,
                row.Day
            })
            .IsUnique()
            .HasDatabaseName("ix_financial_ad_performance_bucket");

        // The dashboard rollup pages on this.
        builder.HasIndex(row => new { row.CampaignId, row.Day })
            .HasDatabaseName("ix_financial_ad_performance_campaign_day");

        builder.HasIndex(row => new { row.PlacementId, row.Day })
            .HasDatabaseName("ix_financial_ad_performance_placement_day");
    }
}

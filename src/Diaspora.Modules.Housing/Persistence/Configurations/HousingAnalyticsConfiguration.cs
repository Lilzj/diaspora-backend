using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the measured record and its daily breakdown — two tables.
/// <para>
/// <b>The composite index on (area, type, occupied) is what makes a neighbourhood market
/// statistic answerable.</b> "What does a two-bedroom flat in Peckham let for, and how long does
/// it take" is the question this whole table exists for, and without that index it is a scan of
/// every property the platform has ever measured.
/// </para>
/// <para>
/// Not soft-deletable, and deliberately: the listing may be, but the measurement of what happened
/// to it is a fact about the past. Deleting it with the listing would quietly rewrite every
/// neighbourhood average that included it.
/// </para>
/// </summary>
public sealed class HousingAnalyticsConfiguration : IEntityTypeConfiguration<HousingAnalytics>
{
    public void Configure(EntityTypeBuilder<HousingAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.AnalyticsTable, HousingModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(HousingValueConverters.Id<HousingAnalyticsId>());

        builder.Property(analytics => analytics.ListingId)
            .HasConversion(HousingValueConverters.Id<HousingListingId>())
            .IsRequired();

        builder.Property(analytics => analytics.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(HousingPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(analytics => analytics.AccommodationTypeKey)
            .HasMaxLength(HousingLimits.MaxTypeKey)
            .IsRequired();

        builder.Property(analytics => analytics.LetAtAmount)
            .HasColumnType(HousingPersistenceConstants.MoneyColumnType);

        builder.Property(analytics => analytics.LetAtCurrency)
            .HasMaxLength(HousingPersistenceConstants.CurrencyCodeLength);

        builder.Property(analytics => analytics.LetAtPeriod)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        ConfigureDaily(builder);

        // One record per listing.
        builder.HasIndex(analytics => analytics.ListingId)
            .IsUnique()
            .HasDatabaseName("ix_housing_analytics_listing");

        // The market statistic. See the class summary.
        builder.HasIndex(analytics => new
            {
                analytics.NeighborhoodAreaId,
                analytics.AccommodationTypeKey,
                analytics.FirstOccupiedAt
            })
            .HasDatabaseName("ix_housing_analytics_market");

        builder.HasIndex(analytics =>
                new { analytics.CountryCode, analytics.AccommodationTypeKey })
            .HasDatabaseName("ix_housing_analytics_country_type");

        // A landlord dashboard covering every property they have ever listed.
        builder.HasIndex(analytics => analytics.LandlordId)
            .HasDatabaseName("ix_housing_analytics_landlord");

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.ViewingConversionRate);
        builder.Ignore(analytics => analytics.ReservationCompletionRate);
    }

    private static void ConfigureDaily(EntityTypeBuilder<HousingAnalytics> builder) =>
        builder.OwnsMany(analytics => analytics.Daily, metric =>
        {
            metric.ToTable(
                HousingPersistenceConstants.AnalyticsDailyTable, HousingModuleMetadata.Schema);

            metric.HasKey(value => value.MetricId);

            // Caller-supplied key. Without this EF emits an UPDATE instead of an INSERT.
            metric.Property(value => value.MetricId).ValueGeneratedNever();

            metric.Property(value => value.Day).IsRequired();

            // Declared inside the OwnsMany block: on the outer builder this would target the
            // analytics table and index a column that is not there.
            metric.HasIndex(value => value.Day)
                .HasDatabaseName("ix_housing_analytics_daily_day");
        });
}

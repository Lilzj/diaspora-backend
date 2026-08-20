using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the analytics rollup and its daily series — two tables.
/// <para>
/// <b>Not soft-deletable, deliberately.</b> A rollup outlives the listing's visibility, because a
/// seller's own history — how long their things take to sell, how often an offer converts — is
/// computed from listings they have long since archived. Making it soft-deletable would make a
/// seller's dashboard forget everything they ever sold.
/// </para>
/// <para>
/// <b>The category and country are denormalised onto both tables.</b> That is what makes the
/// brief's category and geographic trends a <c>GROUP BY</c> over one table rather than a join to
/// every listing in the marketplace, which at any real scale is the difference between a dashboard
/// and a timeout.
/// </para>
/// </summary>
public sealed class ListingAnalyticsConfiguration : IEntityTypeConfiguration<ListingAnalytics>
{
    public void Configure(EntityTypeBuilder<ListingAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.AnalyticsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingAnalyticsId>());

        builder.Property(analytics => analytics.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(analytics => analytics.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(analytics => analytics.CategoryKey)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(MarketplacePersistenceConstants.CountryCodeLength)
            .IsRequired();

        // One rollup per listing. "Create it if missing" is a race two concurrent views will lose,
        // and the row it produces twice is the one this constraint refuses.
        builder.HasIndex(analytics => analytics.ListingId)
            .IsUnique()
            .HasDatabaseName("ux_marketplace_analytics_listing");

        builder.HasIndex(analytics => analytics.SellerId)
            .HasDatabaseName("ix_marketplace_analytics_seller");

        builder.HasIndex(analytics => new { analytics.CategoryKey, analytics.CountryCode })
            .HasDatabaseName("ix_marketplace_analytics_trends");

        ConfigureDaily(builder);

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.OfferRate);
        builder.Ignore(analytics => analytics.ConversionRate);
        builder.Ignore(analytics => analytics.ReservationRate);
        builder.Ignore(analytics => analytics.ClickThroughRate);
    }

    private static void ConfigureDaily(EntityTypeBuilder<ListingAnalytics> builder) =>
        builder.OwnsMany(analytics => analytics.Daily, day =>
        {
            day.ToTable(
                MarketplacePersistenceConstants.AnalyticsDailyTable,
                MarketplaceModuleMetadata.Schema);

            // (rollup, day) is the identity: one bucket per listing per day, which is exactly what
            // makes this a daily series rather than a row per view.
            day.WithOwner().HasForeignKey("analytics_id");
            day.HasKey("analytics_id", nameof(ListingDailyMetric.Day));

            day.Property(value => value.Day).IsRequired();
            day.Property(value => value.Views).IsRequired();
            day.Property(value => value.UniqueViews).IsRequired();
            day.Property(value => value.Saves).IsRequired();
            day.Property(value => value.Shares).IsRequired();
            day.Property(value => value.Offers).IsRequired();
            day.Property(value => value.Reservations).IsRequired();
            day.Property(value => value.Sales).IsRequired();
            day.Property(value => value.Impressions).IsRequired();

            // The trend window: every bucket in a date range, across every listing.
            day.HasIndex(value => value.Day)
                .HasDatabaseName("ix_marketplace_analytics_daily_day");
        });
}

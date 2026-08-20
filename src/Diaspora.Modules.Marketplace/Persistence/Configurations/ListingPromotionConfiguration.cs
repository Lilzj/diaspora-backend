using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps promotions.
/// <para>
/// The index on the window is the sweep's work list: a promotion whose window closed but which
/// nobody concluded leaves a featured flag set on a listing that is no longer paying for it.
/// </para>
/// </summary>
public sealed class ListingPromotionConfiguration : IEntityTypeConfiguration<ListingPromotion>
{
    public void Configure(EntityTypeBuilder<ListingPromotion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.PromotionsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(promotion => promotion.Id);

        builder.Property(promotion => promotion.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingPromotionId>());

        builder.Property(promotion => promotion.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(promotion => promotion.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(promotion => promotion.Kind)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(promotion => promotion.GrantedBy)
            .HasConversion(MarketplaceValueConverters.Id<UserId>());

        builder.Property(promotion => promotion.CancelledBy)
            .HasConversion(MarketplaceValueConverters.Id<UserId>());

        builder.Property(promotion => promotion.ExternalReference)
            .HasMaxLength(Domain.Policies.MarketplacePersistenceLimits.ExternalReference);

        builder.HasIndex(promotion => new { promotion.ListingId, promotion.EndsAt })
            .HasDatabaseName("ix_marketplace_promotions_listing_window");

        // The sweep: everything whose window has closed and which is not already cancelled.
        builder.HasIndex(promotion => new { promotion.EndsAt, promotion.IsCancelled })
            .HasDatabaseName("ix_marketplace_promotions_expiry");

        builder.Ignore(promotion => promotion.DomainEvents);
        builder.Ignore(promotion => promotion.GrantsFeaturedPlacement);
    }
}

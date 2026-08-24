using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SellerReview"/>.
/// <para>
/// <b>The unique index on the reservation is the feature, not a tidiness measure.</b> One review
/// per completed purchase is what stops a buyer rating the same seller ten times off one
/// transaction, and an invariant enforced only in a handler is an invariant that holds until two
/// requests arrive at once.
/// </para>
/// </summary>
public sealed class SellerReviewConfiguration : IEntityTypeConfiguration<SellerReview>
{
    public void Configure(EntityTypeBuilder<SellerReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.SellerReviewsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasConversion(MarketplaceValueConverters.Id<SellerReviewId>());

        builder.Property(review => review.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(review => review.ReviewerId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(review => review.ReservationId)
            .HasConversion(MarketplaceValueConverters.Id<ListingReservationId>())
            .IsRequired();

        builder.Property(review => review.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(review => review.Rating).IsRequired();

        builder.Property(review => review.Title)
            .HasMaxLength(SellerReview.MaxTitleLength);

        builder.Property(review => review.Body)
            .HasMaxLength(SellerReview.MaxBodyLength);

        builder.Property(review => review.Response)
            .HasMaxLength(SellerReview.MaxResponseLength);

        builder.Property(review => review.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.CreatedAt).IsRequired();

        // One review per completed purchase. See the class summary.
        builder.HasIndex(review => review.ReservationId)
            .IsUnique()
            .HasDatabaseName("ix_marketplace_seller_reviews_reservation");

        // The rollup's GROUP BY and the review list both run off this one.
        builder.HasIndex(review => new { review.SellerId, review.Status })
            .HasDatabaseName("ix_marketplace_seller_reviews_seller_status");

        builder.HasIndex(review => review.ReviewerId)
            .HasDatabaseName("ix_marketplace_seller_reviews_reviewer");

        builder.Ignore(review => review.CountsTowardsSummary);
        builder.Ignore(review => review.DomainEvents);
    }
}

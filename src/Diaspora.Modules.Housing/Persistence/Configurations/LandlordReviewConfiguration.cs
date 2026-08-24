using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps <see cref="LandlordReview"/>.
/// <para>
/// <b>The unique index on the reservation is the feature, not a tidiness measure.</b> One review
/// per tenancy is what stops a tenant rating the same landlord ten times off one let, and an
/// invariant enforced only in a handler is an invariant that holds until two requests arrive at
/// once.
/// </para>
/// </summary>
public sealed class LandlordReviewConfiguration : IEntityTypeConfiguration<LandlordReview>
{
    public void Configure(EntityTypeBuilder<LandlordReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.LandlordReviewsTable, HousingModuleMetadata.Schema);

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasConversion(HousingValueConverters.Id<LandlordReviewId>());

        builder.Property(review => review.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(review => review.ReviewerId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(review => review.ReservationId)
            .HasConversion(HousingValueConverters.Id<HousingReservationId>())
            .IsRequired();

        builder.Property(review => review.ListingId)
            .HasConversion(HousingValueConverters.Id<HousingListingId>())
            .IsRequired();

        builder.Property(review => review.Rating).IsRequired();

        builder.Property(review => review.Title).HasMaxLength(LandlordReview.MaxTitleLength);
        builder.Property(review => review.Body).HasMaxLength(LandlordReview.MaxBodyLength);
        builder.Property(review => review.Response).HasMaxLength(LandlordReview.MaxResponseLength);

        builder.Property(review => review.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.CreatedAt).IsRequired();

        // One review per tenancy. See the class summary.
        builder.HasIndex(review => review.ReservationId)
            .IsUnique()
            .HasDatabaseName("ix_housing_landlord_reviews_reservation");

        // The rollup's GROUP BY and the review list both run off this one.
        builder.HasIndex(review => new { review.LandlordId, review.Status })
            .HasDatabaseName("ix_housing_landlord_reviews_landlord_status");

        builder.HasIndex(review => review.ReviewerId)
            .HasDatabaseName("ix_housing_landlord_reviews_reviewer");

        builder.Ignore(review => review.CountsTowardsSummary);
        builder.Ignore(review => review.DomainEvents);
    }
}

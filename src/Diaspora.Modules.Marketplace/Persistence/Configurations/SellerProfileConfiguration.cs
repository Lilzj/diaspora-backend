using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the seller profile. One table, because everything it owns is a value object rather than a
/// collection.
/// <para>
/// <b>The unique index on the owner is the invariant, not a convenience.</b> Two profiles for one
/// member would split a seller's history in half, and the half a buyer sees would be the half the
/// seller chose to show them. A check in a handler is a race; a unique index is not.
/// </para>
/// </summary>
public sealed class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.SellersTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(seller => seller.Id);

        builder.Property(seller => seller.Id)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>());

        builder.Property(seller => seller.OwnerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(seller => seller.PrimaryCountryCode)
            .HasMaxLength(MarketplacePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(seller => seller.Status)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(seller => seller.SuspensionReason)
            .HasMaxLength(MarketplaceLimits.MaxModerationNote);

        builder.Property(seller => seller.Revision).IsConcurrencyToken();

        ConfigureIdentity(builder);
        ConfigureSlug(builder);
        ConfigureContact(builder);
        ConfigureTrustMark(builder);
        ConfigureStatistics(builder);

        // One profile per member. See the class summary.
        builder.HasIndex(seller => seller.OwnerUserId)
            .IsUnique()
            .HasDatabaseName("ux_marketplace_sellers_owner");

        builder.HasIndex(seller => seller.ProviderId)
            .HasDatabaseName("ix_marketplace_sellers_provider");

        builder.HasIndex(seller => new { seller.PrimaryCountryCode, seller.Status })
            .HasDatabaseName("ix_marketplace_sellers_country_status");

        builder.Ignore(seller => seller.DomainEvents);
        builder.Ignore(seller => seller.OwnerId);
        builder.Ignore(seller => seller.CanTrade);
        builder.Ignore(seller => seller.IsVerified);

        // The brief's five seller types, computed from three facts this module does not all own.
        // A column would keep saying "Verified Business" after Trust revoked the verification.
        builder.Ignore(seller => seller.SellerType);
    }

    private static void ConfigureIdentity(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.OwnsOne(seller => seller.Identity, identity =>
        {
            identity.Property(value => value.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(MarketplaceLimits.MaxSellerName)
                .IsRequired();

            identity.Property(value => value.LegalName)
                .HasColumnName("legal_name")
                .HasMaxLength(MarketplaceLimits.MaxSellerName);

            identity.Property(value => value.RegistrationNumber)
                .HasColumnName("registration_number")
                .HasMaxLength(MarketplaceLimits.MaxRegistrationNumber);

            identity.Property(value => value.Kind)
                .HasColumnName("seller_kind")
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            identity.Property(value => value.Bio)
                .HasColumnName("bio")
                .HasMaxLength(MarketplaceLimits.MaxSellerBio);

            identity.Ignore(value => value.VerifiableName);

            identity.HasIndex(value => value.DisplayName)
                .HasDatabaseName("ix_marketplace_sellers_display_name");
        });

        builder.Navigation(seller => seller.Identity).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.OwnsOne(seller => seller.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(MarketplaceLimits.MaxSlug)
                .IsRequired();

            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_marketplace_sellers_slug");
        });

        builder.Navigation(seller => seller.Slug).IsRequired();
    }

    private static void ConfigureContact(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.OwnsOne(seller => seller.Contact, contact =>
        {
            contact.Property(value => value.Phone)
                .HasColumnName("contact_phone")
                .HasMaxLength(MarketplaceLimits.MaxContactValue);

            contact.Property(value => value.Email)
                .HasColumnName("contact_email")
                .HasMaxLength(MarketplaceLimits.MaxContactValue);

            contact.Property(value => value.Website)
                .HasColumnName("contact_website")
                .HasMaxLength(MarketplaceLimits.MaxContactValue);

            contact.Property(value => value.Preference)
                .HasColumnName("contact_preference")
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();
        });

        builder.Navigation(seller => seller.Contact).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.OwnsOne(seller => seller.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(MarketplacePersistenceLimits.Badge);

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(MarketplacePersistenceLimits.Reason);

            mark.Property(value => value.VerifiedAt).HasColumnName("verified_at");
            mark.Property(value => value.ChangedAt).HasColumnName("verification_changed_at");

            mark.Ignore(value => value.IsVerified);

            // How a Trust outcome finds the seller that raised the claim.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_marketplace_sellers_verification_id");

            // The "verified sellers only" filter, which the listing query correlates against.
            mark.HasIndex(value => value.State)
                .HasDatabaseName("ix_marketplace_sellers_verification_state");
        });

        builder.Navigation(seller => seller.TrustMark).IsRequired();
    }

    private static void ConfigureStatistics(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.OwnsOne(seller => seller.Statistics, statistics =>
        {
            statistics.Property(value => value.PublishedListings)
                .HasColumnName("stat_published_listings");

            statistics.Property(value => value.SoldListings)
                .HasColumnName("stat_sold_listings");

            statistics.Property(value => value.CompletedReservations)
                .HasColumnName("stat_completed_reservations");

            statistics.Property(value => value.CancelledReservations)
                .HasColumnName("stat_cancelled_reservations");

            statistics.Property(value => value.AcceptedOffers)
                .HasColumnName("stat_accepted_offers");

            // Stored as a sum and a count rather than as an average, because an average cannot be
            // updated without the count.
            statistics.Property(value => value.TotalResponseMinutes)
                .HasColumnName("stat_response_minutes");

            statistics.Property(value => value.RespondedConversations)
                .HasColumnName("stat_responded_conversations");

            statistics.Ignore(value => value.AverageResponseMinutes);
            statistics.Ignore(value => value.CancellationRate);
        });

        builder.Navigation(seller => seller.Statistics).IsRequired();
    }
}

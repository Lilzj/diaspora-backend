using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the listing aggregate and everything it owns — seven tables.
/// <para>
/// <b>Every owned reference is declared required and its scalars nullable where the domain says
/// so.</b> That combination is the only EF Core shape that works for table-sharing dependents, per
/// B6's lesson and the repeats by B7, B8 and B9. There is no optional owned reference on this
/// aggregate at all, which sidesteps the round-trip caveat entirely.
/// </para>
/// <para>
/// <b>No index spans an owned type's columns and the aggregate's own.</b> EF cannot build one — B6's
/// lesson, recorded again by B7, B8 and B9 — so indexes on owned columns (slug, country, city, area
/// path, price, verification state) are declared inside their <c>OwnsOne</c> builders, and indexes
/// on aggregate columns outside.
/// </para>
/// <para>
/// <b>Every owned collection keys on a caller-supplied identifier with
/// <c>ValueGeneratedNever</c>.</b> Not tidiness: EF's convention makes a Guid key store-generated,
/// and a store-generated key already holding a non-default value tells EF the row exists — it emits
/// an UPDATE instead of an INSERT, matches nothing, and surfaces as a bogus concurrency conflict on
/// the seller's first save.
/// </para>
/// <para>
/// <b>Soft delete plus owned types is the combination B1.0.1 exists for.</b> This aggregate is both
/// — six owned collections — and <c>SoftDeleteConverter</c> handles it; nothing here declares the
/// filter or the cascade, deliberately.
/// </para>
/// </summary>
public sealed class MarketplaceListingConfiguration : IEntityTypeConfiguration<MarketplaceListing>
{
    public void Configure(EntityTypeBuilder<MarketplaceListing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.ListingsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(listing => listing.Id);

        builder.Property(listing => listing.Id)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>());

        builder.Property(listing => listing.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(listing => listing.CreatedByUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(listing => listing.CategoryKey)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey)
            .IsRequired();

        ConfigureEnum(builder, listing => listing.Status);
        ConfigureEnum(builder, listing => listing.Visibility);
        ConfigureEnum(builder, listing => listing.ModerationState);
        ConfigureEnum(builder, listing => listing.ListingType);
        ConfigureEnum(builder, listing => listing.Condition);
        ConfigureEnum(builder, listing => listing.ContactPreference);

        builder.Property(listing => listing.ClosureReason)
            .HasMaxLength(MarketplaceLimits.MaxNoteLength);

        // The optimistic concurrency token. The aggregate bumps it on every content mutation and
        // EnsureRevision rejects a stale editor before the database has to; the token is the
        // backstop for the write that raced anyway.
        builder.Property(listing => listing.Revision).IsConcurrencyToken();

        ConfigureTitle(builder);
        ConfigureSlug(builder);
        ConfigureDescription(builder);
        ConfigurePrice(builder);
        ConfigureInventory(builder);
        ConfigureLocation(builder);
        ConfigureFulfillment(builder);
        ConfigureProduct(builder);
        ConfigureTrustMark(builder);
        ConfigureEngagement(builder);

        ConfigureAttributes(builder);
        ConfigureTags(builder);
        ConfigureMedia(builder);
        ConfigureProviderReferences(builder);
        ConfigureGuideReferences(builder);
        ConfigureModerationNotes(builder);

        ConfigureIndexes(builder);

        builder.Ignore(listing => listing.DomainEvents);
        builder.Ignore(listing => listing.OwnerId);
        builder.Ignore(listing => listing.IsPublishable);
        builder.Ignore(listing => listing.IsDiscoverable);
        builder.Ignore(listing => listing.CoverImage);
    }

    private static void ConfigureEnum<TProperty>(
        EntityTypeBuilder<MarketplaceListing> builder,
        System.Linq.Expressions.Expression<Func<MarketplaceListing, TProperty>> property) =>
        builder.Property(property)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

    private static void ConfigureTitle(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(MarketplaceLimits.MaxTitle)
                .IsRequired();

            // Free-text search's primary target, and the duplicate check's. Indexed here because
            // the column is owned.
            title.HasIndex(value => value.Value).HasDatabaseName("ix_marketplace_listings_title");
        });

        builder.Navigation(listing => listing.Title).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(MarketplaceLimits.MaxSlug)
                .IsRequired();

            // Unique, because it is the deep-link key: a duplicate makes one of two listings
            // permanently unreachable by link. It is also the cursor tie-break — the primary key is
            // a value-converted identifier, which EF cannot compare in SQL.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_marketplace_listings_slug");
        });

        builder.Navigation(listing => listing.Slug).IsRequired();
    }

    private static void ConfigureDescription(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Description, description =>
        {
            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(MarketplaceLimits.MaxDescription)
                .IsRequired();

            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(MarketplaceLimits.MaxSummary)
                .IsRequired();
        });

        builder.Navigation(listing => listing.Description).IsRequired();
    }

    private static void ConfigurePrice(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Price, price =>
        {
            price.Property(value => value.IsDisclosed)
                .HasColumnName("price_disclosed")
                .IsRequired();

            price.Property(value => value.Amount)
                .HasColumnName("price_amount")
                .HasColumnType(MarketplacePersistenceConstants.MoneyColumnType);

            price.Property(value => value.CurrencyCode)
                .HasColumnName("price_currency")
                .HasMaxLength(MarketplacePersistenceConstants.CurrencyCodeLength);

            price.Property(value => value.IsNegotiable)
                .HasColumnName("price_negotiable")
                .IsRequired();

            price.Property(value => value.MinimumOffer)
                .HasColumnName("price_minimum_offer")
                .HasColumnType(MarketplacePersistenceConstants.MoneyColumnType);

            // A price-range filter is always paired with a currency, because nothing in this
            // module converts between them. The composite index is what makes that pairing cheap
            // rather than a scan.
            price.HasIndex(value => new { value.CurrencyCode, value.Amount })
                .HasDatabaseName("ix_marketplace_listings_price");
        });

        builder.Navigation(listing => listing.Price).IsRequired();
    }

    private static void ConfigureInventory(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Inventory, inventory =>
        {
            inventory.Property(value => value.Quantity).HasColumnName("stock_quantity").IsRequired();
            inventory.Property(value => value.Reserved).HasColumnName("stock_reserved").IsRequired();
            inventory.Property(value => value.Sold).HasColumnName("stock_sold").IsRequired();
            inventory.Property(value => value.IsTracked).HasColumnName("stock_tracked").IsRequired();

            // Available is derived from the other three. Persisting it would be a fourth number
            // that can disagree with them, and the disagreement is two buyers sold one phone.
            inventory.Ignore(value => value.Available);
            inventory.Ignore(value => value.HasStock);
            inventory.Ignore(value => value.IsSoldOut);
            inventory.Ignore(value => value.IsFullyReserved);
        });

        builder.Navigation(listing => listing.Inventory).IsRequired();
    }

    private static void ConfigureLocation(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(MarketplacePersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.RegionName)
                .HasColumnName("region_name")
                .HasMaxLength(MarketplaceLimits.MaxLocationName);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(MarketplaceLimits.MaxLocationName);

            location.Property(value => value.Neighbourhood)
                .HasColumnName("neighbourhood")
                .HasMaxLength(MarketplaceLimits.MaxLocationName);

            location.Property(value => value.AreaId).HasColumnName("area_id");

            // A plain string, prefix-matchable with LIKE 'prefix%' — B4's EF lesson. The
            // repository's scope filter runs StartsWith against this column.
            location.Property(value => value.AreaPath)
                .HasColumnName("area_path")
                .HasMaxLength(MarketplaceLimits.MaxAreaPath);

            location.Ignore(value => value.DisplayName);

            // "What is for sale in Germany" is the single most common browse in the module.
            location.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_marketplace_listings_country");

            location.HasIndex(value => value.CityName)
                .HasDatabaseName("ix_marketplace_listings_city");

            location.HasIndex(value => value.AreaPath)
                .HasDatabaseName("ix_marketplace_listings_area_path");
        });

        builder.Navigation(listing => listing.Location).IsRequired();
    }

    private static void ConfigureFulfillment(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Fulfillment, fulfillment =>
        {
            fulfillment.Property(value => value.OffersPickup)
                .HasColumnName("offers_pickup").IsRequired();

            fulfillment.Property(value => value.OffersDelivery)
                .HasColumnName("offers_delivery").IsRequired();

            fulfillment.Property(value => value.OffersShipping)
                .HasColumnName("offers_shipping").IsRequired();

            fulfillment.Property(value => value.IsDigital)
                .HasColumnName("is_digital").IsRequired();

            fulfillment.Property(value => value.DeliveryRadiusKm)
                .HasColumnName("delivery_radius_km");

            fulfillment.Property(value => value.Notes)
                .HasColumnName("fulfillment_notes")
                .HasMaxLength(MarketplaceLimits.MaxNoteLength);

            fulfillment.Ignore(value => value.IsSpecified);

            // The two filters buyers on this platform reach for first, and both are owned columns
            // — so the indexes live here, where EF can actually build them.
            fulfillment.HasIndex(value => value.OffersDelivery)
                .HasDatabaseName("ix_marketplace_listings_delivery");

            fulfillment.HasIndex(value => value.OffersShipping)
                .HasDatabaseName("ix_marketplace_listings_shipping");
        });

        builder.Navigation(listing => listing.Fulfillment).IsRequired();
    }

    private static void ConfigureProduct(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Product, product =>
        {
            product.Property(value => value.Brand)
                .HasColumnName("brand")
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            product.Property(value => value.Model)
                .HasColumnName("model")
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            product.Property(value => value.Sku)
                .HasColumnName("sku")
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            product.Property(value => value.Year).HasColumnName("product_year");

            product.Ignore(value => value.IsSpecified);

            // "Samsung", "Galaxy S21" narrows a category far better than any text search over a
            // description, so brand is a first-class filter.
            product.HasIndex(value => value.Brand)
                .HasDatabaseName("ix_marketplace_listings_brand");
        });

        builder.Navigation(listing => listing.Product).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.TrustMark, mark =>
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

            // The staleness guard for at-least-once, unordered Trust deliveries. Persisted,
            // because the guard is only as durable as the timestamp it compares against.
            mark.Property(value => value.ChangedAt).HasColumnName("verification_changed_at");

            mark.Ignore(value => value.IsVerified);

            // How a Trust outcome finds the listing that raised the claim.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_marketplace_listings_verification_id");
        });

        builder.Navigation(listing => listing.TrustMark).IsRequired();
    }

    private static void ConfigureEngagement(EntityTypeBuilder<MarketplaceListing> builder)
    {
        builder.OwnsOne(listing => listing.Engagement, engagement =>
        {
            engagement.Property(value => value.ViewCount).HasColumnName("view_count");
            engagement.Property(value => value.UniqueViewCount).HasColumnName("unique_view_count");
            engagement.Property(value => value.SaveCount).HasColumnName("save_count");
            engagement.Property(value => value.ShareCount).HasColumnName("share_count");
            engagement.Property(value => value.OfferCount).HasColumnName("offer_count");
            engagement.Property(value => value.ReservationCount).HasColumnName("reservation_count");
            engagement.Property(value => value.SearchImpressions).HasColumnName("search_impressions");

            // The "most viewed" sort.
            engagement.HasIndex(value => value.ViewCount)
                .HasDatabaseName("ix_marketplace_listings_view_count");
        });

        builder.Navigation(listing => listing.Engagement).IsRequired();
    }

    private static void ConfigureAttributes(EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.Attributes, attribute =>
        {
            attribute.ToTable(
                MarketplacePersistenceConstants.ListingAttributesTable,
                MarketplaceModuleMetadata.Schema);

            // The key is (listing, attribute key) rather than a surrogate: a listing cannot state
            // the same attribute twice, and the composite key is what makes that true in the
            // database as well as in the aggregate.
            attribute.WithOwner().HasForeignKey("listing_id");
            attribute.HasKey("listing_id", nameof(ListingAttribute.Key));

            attribute.Property(value => value.Key)
                .HasMaxLength(MarketplaceLimits.MaxAttributeKey)
                .IsRequired();

            attribute.Property(value => value.Value)
                .HasMaxLength(MarketplaceLimits.MaxAttributeValue)
                .IsRequired();

            attribute.Property(value => value.NumericValue)
                .HasColumnType(MarketplacePersistenceConstants.AttributeNumberColumnType);

            attribute.Property(value => value.DisplayName)
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            attribute.Property(value => value.Order).IsRequired();

            // "Listings with this attribute at this value" is the structured filter the whole
            // attribute model exists to make fast.
            attribute.HasIndex(value => new { value.Key, value.Value })
                .HasDatabaseName("ix_marketplace_listing_attributes_key_value");

            // "Under 100,000 miles" is a range over the parsed column, not the string one.
            attribute.HasIndex(value => new { value.Key, value.NumericValue })
                .HasDatabaseName("ix_marketplace_listing_attributes_key_number");
        });

    private static void ConfigureTags(EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.Tags, tag =>
        {
            tag.ToTable(
                MarketplacePersistenceConstants.ListingTagsTable,
                MarketplaceModuleMetadata.Schema);

            tag.WithOwner().HasForeignKey("listing_id");
            tag.HasKey("listing_id", nameof(ListingTag.Slug));

            tag.Property(value => value.Slug)
                .HasMaxLength(MarketplaceLimits.MaxTagLength)
                .IsRequired();

            tag.Property(value => value.Order).IsRequired();

            tag.HasIndex(value => value.Slug).HasDatabaseName("ix_marketplace_listing_tags_slug");
        });

    private static void ConfigureMedia(EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.MediaReferences, reference =>
        {
            reference.ToTable(
                MarketplacePersistenceConstants.ListingMediaTable,
                MarketplaceModuleMetadata.Schema);

            // (media, role) is the identity the aggregate treats as unique, so it is the key.
            // That is what makes attaching idempotent in the database rather than by convention.
            reference.WithOwner().HasForeignKey("listing_id");
            reference.HasKey(
                "listing_id",
                nameof(ListingMediaReference.MediaId),
                nameof(ListingMediaReference.Role));

            reference.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            reference.Property(value => value.Caption)
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureProviderReferences(
        EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.ProviderReferences, reference =>
        {
            reference.ToTable(
                MarketplacePersistenceConstants.ListingProviderReferencesTable,
                MarketplaceModuleMetadata.Schema);

            reference.WithOwner().HasForeignKey("listing_id");
            reference.HasKey("listing_id", nameof(ListingProviderReference.ProviderId));

            // The identifier half of this key is supplied by the caller, and EF marks a Guid key
            // property store-generated by convention. Left alone, EF reads a set key as "this row
            // already exists" and emits an UPDATE where an INSERT was meant, which surfaces as a
            // bogus concurrency conflict rather than a missing row.
            reference.Property(value => value.ProviderId).ValueGeneratedNever();

            reference.Property(value => value.Role)
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            reference.Property(value => value.Note)
                .HasMaxLength(MarketplaceLimits.MaxNoteLength);

            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureGuideReferences(EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.GuideReferences, reference =>
        {
            reference.ToTable(
                MarketplacePersistenceConstants.ListingGuideReferencesTable,
                MarketplaceModuleMetadata.Schema);

            reference.WithOwner().HasForeignKey("listing_id");
            reference.HasKey("listing_id", nameof(ListingGuideReference.GuideId));

            // Caller-supplied key half. See the provider references above.
            reference.Property(value => value.GuideId).ValueGeneratedNever();

            reference.Property(value => value.Label)
                .HasMaxLength(MarketplaceLimits.MaxDisplayName);

            reference.Property(value => value.IsAutomatic).IsRequired();
            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureModerationNotes(EntityTypeBuilder<MarketplaceListing> builder) =>
        builder.OwnsMany(listing => listing.ModerationNotes, note =>
        {
            note.ToTable(
                MarketplacePersistenceConstants.ListingModerationNotesTable,
                MarketplaceModuleMetadata.Schema);

            // The identifier is caller-supplied and ValueGeneratedNever is load-bearing. See the
            // class summary.
            note.HasKey(value => value.NoteId);
            note.Property(value => value.NoteId).ValueGeneratedNever();

            note.Property(value => value.AuthorId)
                .HasConversion(MarketplaceValueConverters.Id<UserId>())
                .IsRequired();

            note.Property(value => value.Note)
                .HasMaxLength(MarketplaceLimits.MaxModerationNote)
                .IsRequired();

            note.Property(value => value.CreatedAt).IsRequired();
        });

    private static void ConfigureIndexes(EntityTypeBuilder<MarketplaceListing> builder)
    {
        // The seller dashboard's every tab.
        builder.HasIndex(listing => new { listing.SellerId, listing.Status })
            .HasDatabaseName("ix_marketplace_listings_seller_status");

        // The default browse: newest published first, within a status.
        builder.HasIndex(listing => new { listing.Status, listing.PublishedAt })
            .HasDatabaseName("ix_marketplace_listings_status_published");

        builder.HasIndex(listing => listing.CategoryKey)
            .HasDatabaseName("ix_marketplace_listings_category");

        // The expiry sweep's work list.
        builder.HasIndex(listing => listing.ExpiresAt)
            .HasDatabaseName("ix_marketplace_listings_expires_at");

        builder.HasIndex(listing => listing.PublishedAt)
            .HasDatabaseName("ix_marketplace_listings_published_at");

        builder.HasIndex(listing => listing.IsFeatured)
            .HasDatabaseName("ix_marketplace_listings_featured");

        // The moderation queue reads by state and sorts by report count.
        builder.HasIndex(listing => new { listing.ModerationState, listing.ReportCount })
            .HasDatabaseName("ix_marketplace_listings_moderation");
    }
}

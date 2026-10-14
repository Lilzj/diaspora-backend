using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the listing aggregate and the six tables it owns.
/// <para>
/// <b>The indexes here are the module's read performance, and three of them are load-bearing.</b>
/// The neighbourhood index is what makes "every property in Peckham" a seek rather than a scan and
/// is the single most-run query in the module. The area-path index is its subtree equivalent —
/// B4's materialised path stored as text precisely so "everything under London" is an indexed
/// <c>LIKE 'prefix%'</c>. And the availability index is the sweep's work list: without it, finding
/// the properties whose move-in date arrived today means reading every listing ever created.
/// </para>
/// <para>
/// <b>Owned-collection indexes must be declared inside the <c>OwnsMany</c> block.</b> Declaring
/// them on the outer builder silently targets the parent table and produces an index over a column
/// that is not there — the trap B6 records and every module since has had to remember.
/// </para>
/// <para>
/// Nothing here declares the soft-delete filter or the delete cascade for owned rows. The shared
/// context applies the global query filter and <c>SoftDeleteConverter</c> handles owned rows —
/// B1.0.1 exists so a module never has to.
/// </para>
/// </summary>
public sealed class HousingListingConfiguration : IEntityTypeConfiguration<HousingListing>
{
    public void Configure(EntityTypeBuilder<HousingListing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.ListingsTable, HousingModuleMetadata.Schema);

        builder.HasKey(listing => listing.Id);

        // The optimistic concurrency token, and it has to be declared here rather than merely
        // bumped in the aggregate. EnsureRevision rejects a stale editor that sends back the
        // revision it loaded; nothing stops the editor that does not send one, and two saves from
        // one landlord's two devices would then both commit with the later one silently winning.
        // Declaring the token makes the database arbitrate, which is the only place it can be
        // settled. B10 learnt this; B11 shipped without it until an integration test raced two
        // contexts and found no exception at all.
        builder.Property(listing => listing.Revision).IsConcurrencyToken();

        builder.Property(listing => listing.Id)
            .HasConversion(HousingValueConverters.Id<HousingListingId>());

        builder.Property(listing => listing.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(listing => listing.CreatedByUserId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(listing => listing.AccommodationTypeKey)
            .HasMaxLength(HousingLimits.MaxTypeKey)
            .IsRequired();

        ConfigureContent(builder);
        ConfigureEnums(builder);
        ConfigureOwnedValues(builder);
        ConfigureOwnedCollections(builder);
        ConfigureIndexes(builder);

        builder.Ignore(listing => listing.DomainEvents);
        builder.Ignore(listing => listing.CoverImage);
        builder.Ignore(listing => listing.AcceptsViewings);
        builder.Ignore(listing => listing.IsPublishable);
    }

    private static void ConfigureContent(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(HousingLimits.MaxTitle)
                .IsRequired();
        });

        builder.Navigation(listing => listing.Title).IsRequired();

        builder.OwnsOne(listing => listing.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(HousingLimits.MaxSlug)
                .IsRequired();

            // Unique across deleted rows too: a deleted listing keeps its slug, because the links
            // shared to it are in emails between people arranging to move.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_housing_listings_slug");
        });

        builder.Navigation(listing => listing.Slug).IsRequired();

        builder.OwnsOne(listing => listing.Description, description =>
        {
            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(HousingLimits.MaxSummary)
                .IsRequired();

            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(HousingLimits.MaxDescription);
        });

        builder.Navigation(listing => listing.Description).IsRequired();
    }

    private static void ConfigureEnums(EntityTypeBuilder<HousingListing> builder)
    {
        builder.Property(listing => listing.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.Visibility)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.ModerationState)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.LeaseType)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.FurnishedStatus)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.Parking)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.SmokingPolicy)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(listing => listing.PetPolicy)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();
    }

    private static void ConfigureOwnedValues(EntityTypeBuilder<HousingListing> builder)
    {
        ConfigurePrice(builder);
        ConfigureCapacity(builder);
        ConfigureAvailability(builder);
        ConfigureLocation(builder);
        ConfigureAddress(builder);
        ConfigureStayPolicy(builder);
        ConfigureRoommatePreferences(builder);
        ConfigureTrustAndEngagement(builder);
    }

    private static void ConfigurePrice(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Price, price =>
        {
            price.Property(value => value.IsDisclosed)
                .HasColumnName("price_disclosed")
                .IsRequired();

            price.Property(value => value.Amount)
                .HasColumnName("rent_amount")
                .HasColumnType(HousingPersistenceConstants.MoneyColumnType);

            price.Property(value => value.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(HousingPersistenceConstants.CurrencyCodeLength);

            price.Property(value => value.Period)
                .HasColumnName("rent_period")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            price.Property(value => value.DepositAmount)
                .HasColumnName("deposit_amount")
                .HasColumnType(HousingPersistenceConstants.MoneyColumnType);

            price.Property(value => value.IsNegotiable).HasColumnName("rent_negotiable");
            price.Property(value => value.UtilitiesIncluded).HasColumnName("utilities_included");
            price.Property(value => value.InternetIncluded).HasColumnName("internet_included");

            price.Ignore(value => value.DepositRequired);
            price.Ignore(value => value.MoveInTotal);

            // A price-range filter is currency-and-period scoped, so the index is too. Indexing
            // the amount alone would serve a query the module deliberately refuses to answer.
            price.HasIndex(value =>
                    new { value.CurrencyCode, value.Period, value.Amount })
                .HasDatabaseName("ix_housing_listings_rent");
        });

        builder.Navigation(listing => listing.Price).IsRequired();
    }

    private static void ConfigureCapacity(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Capacity, capacity =>
        {
            capacity.Property(value => value.Bedrooms).HasColumnName("bedrooms").IsRequired();

            capacity.Property(value => value.Bathrooms)
                .HasColumnName("bathrooms")
                .HasColumnType(HousingPersistenceConstants.BathroomColumnType)
                .IsRequired();

            capacity.Property(value => value.MaximumOccupants).HasColumnName("max_occupants");

            capacity.Property(value => value.FloorAreaSquareMetres)
                .HasColumnName("floor_area_sqm");

            capacity.Ignore(value => value.IsStudio);

            // Bedrooms first: it is the filter nearly every search applies and the one with the
            // most selective distribution.
            capacity.HasIndex(value => new { value.Bedrooms, value.Bathrooms })
                .HasDatabaseName("ix_housing_listings_capacity");
        });

        builder.Navigation(listing => listing.Capacity).IsRequired();
    }

    private static void ConfigureAvailability(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Availability, availability =>
        {
            availability.Property(value => value.AvailableFrom).HasColumnName("available_from");
            availability.Property(value => value.AvailableUntil).HasColumnName("available_until");

            availability.Property(value => value.TotalUnits)
                .HasColumnName("total_units")
                .IsRequired();

            availability.Property(value => value.ReservedUnits)
                .HasColumnName("reserved_units")
                .IsRequired();

            availability.Property(value => value.OccupiedUnits)
                .HasColumnName("occupied_units")
                .IsRequired();

            availability.Property(value => value.MinimumStayNights)
                .HasColumnName("min_stay_nights");

            availability.Property(value => value.MaximumStayNights)
                .HasColumnName("max_stay_nights");

            availability.Ignore(value => value.AvailableUnits);
            availability.Ignore(value => value.HasUnitsRemaining);
            availability.Ignore(value => value.IsFullyCommitted);
            availability.Ignore(value => value.IsFullyOccupied);
            availability.Ignore(value => value.IsSingleUnit);

            // The availability sweep's work list, ordered. Without it, finding the properties
            // whose move-in date arrived today reads every listing ever created.
            availability.HasIndex(value => value.AvailableFrom)
                .HasDatabaseName("ix_housing_listings_available_from");
        });

        builder.Navigation(listing => listing.Availability).IsRequired();
    }

    private static void ConfigureLocation(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(HousingPersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.RegionName)
                .HasColumnName("region_name")
                .HasMaxLength(HousingLimits.MaxLocationName);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(HousingLimits.MaxLocationName);

            location.Property(value => value.NeighborhoodName)
                .HasColumnName("neighborhood_name")
                .HasMaxLength(HousingLimits.MaxLocationName);

            location.Property(value => value.NeighborhoodAreaId)
                .HasColumnName("neighborhood_area_id");

            location.Property(value => value.AreaPath)
                .HasColumnName("area_path")
                .HasMaxLength(HousingLimits.MaxAreaPath);

            // The module's most-run query: every property in one neighbourhood.
            location.HasIndex(value => value.NeighborhoodAreaId)
                .HasDatabaseName("ix_housing_listings_neighborhood");

            // Its subtree equivalent. B4's materialised path stored as text precisely so this is
            // an indexed prefix scan rather than a recursive descent.
            location.HasIndex(value => value.AreaPath)
                .HasDatabaseName("ix_housing_listings_area_path");

            location.HasIndex(value => new { value.CountryCode, value.CityName })
                .HasDatabaseName("ix_housing_listings_country_city");

            location.Ignore(value => value.DisplayName);
            location.Ignore(value => value.HasNeighborhood);
        });

        builder.Navigation(listing => listing.Location).IsRequired();
    }

    private static void ConfigureAddress(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.Address, address =>
        {
            address.Property(value => value.Line1)
                .HasColumnName("address_line1")
                .HasMaxLength(HousingLimits.MaxAddressLine);

            address.Property(value => value.Line2)
                .HasColumnName("address_line2")
                .HasMaxLength(HousingLimits.MaxAddressLine);

            address.Property(value => value.PostalCode)
                .HasColumnName("postal_code")
                .HasMaxLength(HousingLimits.MaxPostalCode);

            address.Property(value => value.Latitude)
                .HasColumnName("latitude")
                .HasColumnType(HousingPersistenceConstants.CoordinateColumnType);

            address.Property(value => value.Longitude)
                .HasColumnName("longitude")
                .HasColumnType(HousingPersistenceConstants.CoordinateColumnType);

            address.Property(value => value.Visibility)
                .HasColumnName("address_visibility")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            address.Ignore(value => value.HasCoordinate);

            // The bounding-box prefilter's index, as on providers. A composite on both axes,
            // because a query that narrows latitude and then filters longitude in memory reads a
            // band around the whole planet.
            address.HasIndex(value => new { value.Latitude, value.Longitude })
                .HasDatabaseName("ix_housing_listings_coordinates");
        });

        builder.Navigation(listing => listing.Address).IsRequired();
    }

    private static void ConfigureStayPolicy(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.StayPolicy, policy =>
        {
            policy.Property(value => value.AcceptsShortStay).HasColumnName("short_stay");
            policy.Property(value => value.AcceptsLongStay).HasColumnName("long_stay");
            policy.Property(value => value.IsStudentFriendly).HasColumnName("student_friendly");
            policy.Property(value => value.IsFamilyFriendly).HasColumnName("family_friendly");
            policy.Property(value => value.IsSharedAccommodation).HasColumnName("shared");
            policy.Property(value => value.IsLookingForRoommate).HasColumnName("roommate_wanted");
        });

        builder.Navigation(listing => listing.StayPolicy).IsRequired();
    }

    private static void ConfigureRoommatePreferences(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.RoommatePreferences, preferences =>
        {
            preferences.Property(value => value.GenderPreference)
                .HasColumnName("roommate_gender")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            preferences.Property(value => value.Occupation)
                .HasColumnName("roommate_occupation")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            preferences.Property(value => value.MinimumAge).HasColumnName("roommate_min_age");
            preferences.Property(value => value.MaximumAge).HasColumnName("roommate_max_age");

            preferences.Property(value => value.SmokingPolicy)
                .HasColumnName("roommate_smoking")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            preferences.Property(value => value.PetPolicy)
                .HasColumnName("roommate_pets")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            preferences.Property(value => value.NoiseTolerance)
                .HasColumnName("roommate_noise")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            preferences.Property(value => value.NationalityPreference)
                .HasColumnName("roommate_nationality")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            preferences.Property(value => value.LanguagesCsv)
                .HasColumnName("roommate_languages")
                .HasMaxLength(
                    HousingLimits.MaxRoommateLanguages * (HousingLimits.MaxLanguageName + 1));

            // Not mapped: projected from the CSV column.
            preferences.Ignore(value => value.Languages);
            preferences.Ignore(value => value.IsEmpty);
            preferences.Ignore(value => value.IsHouseholdCompositionOnly);
        });

        builder.Navigation(listing => listing.RoommatePreferences).IsRequired();
    }

    private static void ConfigureTrustAndEngagement(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsOne(listing => listing.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            mark.Property(value => value.DecidedAt).HasColumnName("verification_decided_at");

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(HousingLimits.MaxNoteLength);

            // The correlation key a Trust outcome arrives against.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_housing_listings_verification");

            mark.Ignore(value => value.IsVerified);
            mark.Ignore(value => value.IsPending);
        });

        builder.Navigation(listing => listing.TrustMark).IsRequired();

        builder.OwnsOne(listing => listing.Engagement, engagement =>
        {
            engagement.Property(value => value.ViewCount).HasColumnName("view_count");
            engagement.Property(value => value.UniqueViewCount).HasColumnName("unique_view_count");
            engagement.Property(value => value.SaveCount).HasColumnName("save_count");

            engagement.Property(value => value.ViewingRequestCount)
                .HasColumnName("viewing_request_count");

            engagement.Property(value => value.ReservationCount)
                .HasColumnName("reservation_count");

            engagement.Property(value => value.EnquiryCount).HasColumnName("enquiry_count");

            engagement.Ignore(value => value.ViewingConversionRate);
        });

        builder.Navigation(listing => listing.Engagement).IsRequired();
    }

    private static void ConfigureOwnedCollections(EntityTypeBuilder<HousingListing> builder)
    {
        builder.OwnsMany(listing => listing.Amenities, amenity =>
        {
            amenity.ToTable(
                HousingPersistenceConstants.ListingAmenitiesTable,
                HousingModuleMetadata.Schema);

            // The amenity key is the identity the aggregate treats as unique — SetAmenities groups
            // by it — so it is the key here too, and a duplicate becomes impossible in the database
            // rather than merely unlikely in code.
            //
            // Declaring a key at all is load-bearing. Left to convention EF invents a shadow
            // integer, marks it store-generated, and the composite primary key it lands in cannot
            // be an identity column: PostgreSQL tolerates it, SQLite refuses the insert outright,
            // and the failure surfaces as "NOT NULL constraint failed" the first time a listing
            // carries one of these rows. Four collections here needed it.
            amenity.WithOwner().HasForeignKey("HousingListingId");
            amenity.HasKey("HousingListingId", nameof(HousingAmenityAssignment.AmenityKey));

            amenity.Property(value => value.AmenityKey)
                .HasMaxLength(HousingLimits.MaxAmenityKey)
                .IsRequired();

            amenity.Property(value => value.Note).HasMaxLength(HousingLimits.MaxDisplayName);

            // Declared inside the OwnsMany block: on the outer builder this would target the
            // listings table and index a column that is not there.
            amenity.HasIndex(value => value.AmenityKey)
                .HasDatabaseName("ix_housing_listing_amenities_key");
        });

        builder.OwnsMany(listing => listing.HouseRules, rule =>
        {
            rule.ToTable(
                HousingPersistenceConstants.ListingHouseRulesTable,
                HousingModuleMetadata.Schema);

            rule.HasKey(value => value.RuleId);

            // Caller-supplied key. Without this EF emits an UPDATE instead of an INSERT and it
            // surfaces as a bogus concurrency conflict rather than a missing row.
            rule.Property(value => value.RuleId).ValueGeneratedNever();

            rule.Property(value => value.Text)
                .HasMaxLength(HousingLimits.MaxHouseRuleLength)
                .IsRequired();
        });

        builder.OwnsMany(listing => listing.MediaReferences, media =>
        {
            media.ToTable(
                HousingPersistenceConstants.ListingMediaTable, HousingModuleMetadata.Schema);

            // (media, role) is the identity AttachMedia treats as unique, so it is the key. That is
            // what makes attaching idempotent in the database rather than by convention.
            media.WithOwner().HasForeignKey("HousingListingId");
            media.HasKey(
                "HousingListingId",
                nameof(HousingMediaReference.MediaId),
                nameof(HousingMediaReference.Role));

            media.Property(value => value.MediaId).IsRequired();

            media.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            media.Property(value => value.Caption).HasMaxLength(HousingLimits.MaxDisplayName);

            media.Ignore(value => value.IsDocument);

            media.HasIndex(value => value.MediaId)
                .HasDatabaseName("ix_housing_listing_media_asset");
        });

        builder.OwnsMany(listing => listing.ProviderReferences, provider =>
        {
            provider.ToTable(
                HousingPersistenceConstants.ListingProviderReferencesTable,
                HousingModuleMetadata.Schema);

            // One provider is attached to one property once, whatever role it plays.
            provider.WithOwner().HasForeignKey("HousingListingId");
            provider.HasKey("HousingListingId", nameof(HousingProviderReference.ProviderId));

            // The identifier half of this key is supplied by the caller, and EF marks a Guid key
            // property store-generated by convention. Left alone, EF reads a set key as "this row
            // already exists" and emits an UPDATE where an INSERT was meant, which surfaces as a
            // bogus concurrency conflict rather than a missing row.
            provider.Property(value => value.ProviderId).ValueGeneratedNever().IsRequired();

            provider.Property(value => value.ProviderName)
                .HasMaxLength(HousingLimits.MaxDisplayName);

            provider.Property(value => value.Role).HasMaxLength(HousingLimits.MaxDisplayName);

            provider.HasIndex(value => value.ProviderId)
                .HasDatabaseName("ix_housing_listing_providers_provider");
        });

        builder.OwnsMany(listing => listing.GuideReferences, guide =>
        {
            guide.ToTable(
                HousingPersistenceConstants.ListingGuideReferencesTable,
                HousingModuleMetadata.Schema);

            guide.WithOwner().HasForeignKey("HousingListingId");
            guide.HasKey("HousingListingId", nameof(HousingGuideReference.GuideId));

            // Caller-supplied key half. See the provider references above.
            guide.Property(value => value.GuideId).ValueGeneratedNever().IsRequired();
            guide.Property(value => value.Title).HasMaxLength(HousingLimits.MaxDisplayName);
            guide.Property(value => value.Slug).HasMaxLength(HousingLimits.MaxSlug);
        });

        builder.OwnsMany(listing => listing.ModerationNotes, note =>
        {
            note.ToTable(
                HousingPersistenceConstants.ListingModerationNotesTable,
                HousingModuleMetadata.Schema);

            note.HasKey(value => value.NoteId);
            note.Property(value => value.NoteId).ValueGeneratedNever();

            note.Property(value => value.AuthorId)
                .HasConversion(HousingValueConverters.Id<UserId>())
                .IsRequired();

            note.Property(value => value.Note)
                .HasMaxLength(HousingLimits.MaxModerationNote)
                .IsRequired();

            note.Property(value => value.CreatedAt).IsRequired();
        });
    }

    private static void ConfigureIndexes(EntityTypeBuilder<HousingListing> builder)
    {
        // The landlord's own dashboard, and the abuse bound's count.
        builder.HasIndex(listing => new { listing.LandlordId, listing.Status })
            .HasDatabaseName("ix_housing_listings_landlord_status");

        // The public browse predicate, in the order it narrows.
        builder.HasIndex(listing =>
                new { listing.Status, listing.Visibility, listing.ModerationState })
            .HasDatabaseName("ix_housing_listings_discovery");

        builder.HasIndex(listing => listing.AccommodationTypeKey)
            .HasDatabaseName("ix_housing_listings_type");

        // The default sort, and the cursor's first comparison.
        builder.HasIndex(listing => listing.PublishedAt)
            .HasDatabaseName("ix_housing_listings_published_at");

        builder.HasIndex(listing => listing.ReportCount)
            .HasDatabaseName("ix_housing_listings_report_count");
    }
}

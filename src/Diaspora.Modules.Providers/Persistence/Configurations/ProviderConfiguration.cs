using Diaspora.Modules.Providers.Domain.Availability;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Services;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Domain.Verification;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>
/// Maps the provider aggregate and everything it owns — fifteen tables.
/// <para>
/// <b>Every owned reference is declared required, and every one of their properties is
/// nullable.</b> That combination is not a contradiction; it is the only EF Core shape that
/// works. An <em>optional</em> owned reference whose properties are all nullable is the model
/// that cannot distinguish an absent instance from a present-but-empty one, and EF fails at
/// model-build time rather than at run time. B6 recorded exactly this over its post location;
/// B7 has five such references and would have hit it five times.
/// </para>
/// <para>
/// <b>The indexes are chosen from the queries, not from the columns.</b> Discovery filters on
/// status and orders by rating or publication, so those pairs come first; the geographic prefix
/// index is what makes "everything in London" a range scan rather than a table scan; and the
/// slug index is unique because it is the deep-link key and a duplicate would make one of two
/// providers unreachable.
/// </para>
/// <para>
/// <b>Soft delete plus owned types is the combination B1.0.1 exists for.</b> This aggregate is
/// both — six owned collections, one of them nested two deep — which before that patch would
/// have silently deleted every child row on a soft delete, or failed on a NOT NULL constraint.
/// <c>SoftDeleteConverter</c> handles it; nothing here has to work around it, and the workaround
/// B5 originally shipped is deliberately absent.
/// </para>
/// </summary>
public sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ProviderPersistenceConstants.ProvidersTable, ProvidersModuleMetadata.Schema);

        builder.HasKey(provider => provider.Id);

        builder.Property(provider => provider.Id)
            .HasConversion(ProviderValueConverters.Id<ProviderId>());

        builder.Property(provider => provider.CategoryId)
            .HasConversion(ProviderValueConverters.Id<ServiceCategoryId>())
            .IsRequired();

        builder.Property(provider => provider.Type)
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(provider => provider.Status)
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(provider => provider.DeliveryMode)
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(provider => provider.StatusReason).HasMaxLength(500);

        ConfigureName(builder);
        ConfigureSlug(builder);
        ConfigureProfile(builder);
        ConfigureOrganization(builder);
        ConfigureLocation(builder);
        ConfigureVisibility(builder);
        ConfigureAvailabilitySettings(builder);
        ConfigureStatistics(builder);
        ConfigureReviewSummary(builder);
        ConfigureVerificationSummary(builder);

        ConfigureMembers(builder);
        ConfigureServices(builder);
        ConfigureServiceAreas(builder);
        ConfigureLocations(builder);
        ConfigureLanguages(builder);
        ConfigureContactMethods(builder);
        ConfigureGallery(builder);
        ConfigureCertifications(builder);
        ConfigureTags(builder);
        ConfigureOperatingHours(builder);
        ConfigureAvailabilityExceptions(builder);
        ConfigureVerifications(builder);

        ConfigureIndexes(builder);

        builder.Ignore(provider => provider.DomainEvents);
        builder.Ignore(provider => provider.IsDiscoverable);
        builder.Ignore(provider => provider.IsOperating);
        builder.Ignore(provider => provider.IsVerified);
        builder.Ignore(provider => provider.ServesRemotely);
        builder.Ignore(provider => provider.IsPublishable);
        builder.Ignore(provider => provider.PrimaryOwnerId);
    }

    private static void ConfigureName(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Name, name =>
        {
            name.Property(value => value.Value)
                .HasColumnName("name")
                .HasMaxLength(ProviderName.MaxLength)
                .IsRequired();

            // Discovery's free-text term is matched against the name, so the column carries its
            // own index rather than relying on the aggregate's.
            name.HasIndex(value => value.Value).HasDatabaseName("ix_providers_name");
        });

        builder.Navigation(provider => provider.Name).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(ProviderPersistenceConstants.SlugLength)
                .IsRequired();

            // Unique, because it is the deep-link key: a duplicate makes one of two providers
            // permanently unreachable by link, and the failure is invisible until somebody
            // shares one.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_providers_slug");
        });

        builder.Navigation(provider => provider.Slug).IsRequired();
    }

    private static void ConfigureProfile(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Profile, profile =>
        {
            profile.Property(value => value.Id)
                .HasColumnName("profile_id")
                .HasConversion(ProviderValueConverters.Id<ProviderProfileId>());

            profile.Property(value => value.Tagline)
                .HasColumnName("tagline")
                .HasMaxLength(ProviderProfile.MaxTaglineLength);

            profile.Property(value => value.Mission)
                .HasColumnName("mission")
                .HasMaxLength(ProviderProfile.MaxMissionLength);

            profile.Property(value => value.FoundedOn)
                .HasColumnName("founded_on")
                .HasConversion(ProviderValueConverters.NullableDate);

            profile.Property(value => value.CoverMediaId)
                .HasColumnName("cover_media_id")
                .HasConversion(ProviderValueConverters.Id<MediaId>());

            profile.Property(value => value.LogoMediaId)
                .HasColumnName("logo_media_id")
                .HasConversion(ProviderValueConverters.Id<MediaId>());

            profile.Property(value => value.TypicalResponseHours)
                .HasColumnName("typical_response_hours");

            profile.OwnsOne(value => value.Description, description =>
            {
                description.Property(inner => inner.Body)
                    .HasColumnName("description")
                    .HasMaxLength(ProviderDescription.MaxBodyLength)
                    .IsRequired();

                description.Property(inner => inner.Summary)
                    .HasColumnName("description_summary")
                    .HasMaxLength(ProviderDescription.MaxSummaryLength)
                    .IsRequired();
            });

            profile.Navigation(value => value.Description).IsRequired();
            profile.Ignore(value => value.DomainEvents);
        });

        builder.Navigation(provider => provider.Profile).IsRequired();
    }

    /// <summary>
    /// The one genuinely optional owned reference on the aggregate, and the exception that
    /// proves the rule stated in the class summary: it is safe to leave optional precisely
    /// because <c>ProviderOrganizationId</c> is non-nullable, so EF has a discriminating column
    /// and can tell an absent organization from an empty one.
    /// </summary>
    private static void ConfigureOrganization(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsOne(provider => provider.Organization, organization =>
        {
            organization.Property(value => value.Id)
                .HasColumnName("organization_id")
                .HasConversion(ProviderValueConverters.Id<ProviderOrganizationId>());

            organization.Property(value => value.LegalName)
                .HasColumnName("legal_name")
                .HasMaxLength(ProviderOrganization.MaxLegalNameLength);

            organization.Property(value => value.RegistrationNumber)
                .HasColumnName("registration_number")
                .HasMaxLength(ProviderOrganization.MaxRegistrationNumberLength);

            organization.Property(value => value.RegistrationJurisdiction)
                .HasColumnName("registration_jurisdiction")
                .HasMaxLength(ProviderOrganization.MaxJurisdictionLength);

            organization.Property(value => value.RegisteredOn)
                .HasColumnName("registered_on")
                .HasConversion(ProviderValueConverters.NullableDate);

            organization.Property(value => value.TaxIdentifier)
                .HasColumnName("tax_identifier")
                .HasMaxLength(ProviderOrganization.MaxRegistrationNumberLength);

            organization.Property(value => value.IsNonProfit).HasColumnName("is_non_profit");
            organization.Property(value => value.MembershipSize).HasColumnName("membership_size");
            organization.Property(value => value.StaffSize).HasColumnName("staff_size");

            organization.Ignore(value => value.HasRegistrationEvidence);
            organization.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureLocation(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(ProviderPersistenceConstants.CountryCodeLength);

            location.Property(value => value.AreaId).HasColumnName("area_id");

            location.Property(value => value.AreaName)
                .HasColumnName("area_name")
                .HasMaxLength(ProviderPersistenceConstants.NameLength);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(ProviderPersistenceConstants.NameLength);

            location.Property(value => value.Latitude).HasColumnName("latitude");
            location.Property(value => value.Longitude).HasColumnName("longitude");

            location.Property(value => value.HierarchyPath)
                .HasColumnName("hierarchy_path")
                .HasMaxLength(ProviderPersistenceConstants.HierarchyPathLength);

            location.Property(value => value.AddressLine)
                .HasColumnName("address_line")
                .HasMaxLength(ProviderPersistenceConstants.LongNameLength);

            location.Property(value => value.PostalCode)
                .HasColumnName("postal_code")
                .HasMaxLength(32);

            location.HasIndex(value => value.CountryCode).HasDatabaseName("ix_providers_country");

            // The index that makes "everything in London" a range scan. Without it, geographic
            // discovery is a table scan on every request.
            location.HasIndex(value => value.HierarchyPath).HasDatabaseName("ix_providers_path");

            location.HasIndex(value => value.CityName).HasDatabaseName("ix_providers_city");

            // The bounding-box prefilter's index. A composite on both axes, because a query that
            // narrows latitude and then filters longitude in memory reads a band around the
            // whole planet.
            location.HasIndex(value => new { value.Latitude, value.Longitude })
                .HasDatabaseName("ix_providers_coordinates");
        });

        builder.Navigation(provider => provider.Location).IsRequired();
    }

    private static void ConfigureVisibility(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Visibility, visibility =>
        {
            visibility.Property(value => value.Level)
                .HasColumnName("visibility_level")
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            visibility.Property(value => value.CountryCode)
                .HasColumnName("visibility_country_code")
                .HasMaxLength(ProviderPersistenceConstants.CountryCodeLength);

            visibility.Property(value => value.AreaId).HasColumnName("visibility_area_id");

            visibility.Ignore(value => value.IsDiscoverable);
            visibility.Ignore(value => value.IsPublic);
        });

        builder.Navigation(provider => provider.Visibility).IsRequired();
    }

    private static void ConfigureAvailabilitySettings(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Availability, availability =>
        {
            availability.Property(value => value.Kind)
                .HasColumnName("availability_kind")
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            availability.Property(value => value.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(ProviderPersistenceConstants.TimeZoneLength)
                .IsRequired();

            availability.Property(value => value.AcceptsWalkIns).HasColumnName("accepts_walk_ins");
            availability.Property(value => value.AcceptsAppointments).HasColumnName("accepts_appointments");
            availability.Property(value => value.OffersEmergencyService).HasColumnName("offers_emergency_service");
            availability.Property(value => value.OffersRemoteService).HasColumnName("offers_remote_service");

            availability.Property(value => value.Note)
                .HasColumnName("availability_note")
                .HasMaxLength(AvailabilitySettings.MaxNoteLength);

            availability.Ignore(value => value.HasOpenNowSemantics);
        });

        builder.Navigation(provider => provider.Availability).IsRequired();
    }

    private static void ConfigureStatistics(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.Statistics, statistics =>
        {
            statistics.Property(value => value.ViewCount).HasColumnName("view_count");
            statistics.Property(value => value.ContactCount).HasColumnName("contact_count");
            statistics.Property(value => value.SearchImpressionCount).HasColumnName("search_impression_count");
            statistics.Property(value => value.BookmarkCount).HasColumnName("bookmark_count");
            statistics.Property(value => value.ShareCount).HasColumnName("share_count");
            statistics.Property(value => value.FollowerCount).HasColumnName("follower_count");
            statistics.Property(value => value.PostCount).HasColumnName("post_count");
            statistics.Property(value => value.ServiceCount).HasColumnName("service_count");
            statistics.Property(value => value.MemberCount).HasColumnName("member_count");
            statistics.Property(value => value.ReviewCount).HasColumnName("review_count");
            statistics.Property(value => value.RecommendationCount).HasColumnName("recommendation_count");
            statistics.Property(value => value.ProfileCompletionPercent).HasColumnName("profile_completion_percent");
        });

        builder.Navigation(provider => provider.Statistics).IsRequired();
    }

    private static void ConfigureReviewSummary(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.ReviewSummary, summary =>
        {
            summary.Property(value => value.OneStar).HasColumnName("rating_1_count");
            summary.Property(value => value.TwoStar).HasColumnName("rating_2_count");
            summary.Property(value => value.ThreeStar).HasColumnName("rating_3_count");
            summary.Property(value => value.FourStar).HasColumnName("rating_4_count");
            summary.Property(value => value.FiveStar).HasColumnName("rating_5_count");

            // Derived, and stored anyway: discovery sorts and filters on both, and a computed
            // expression cannot be indexed portably across the providers this platform targets.
            summary.Property(value => value.TotalReviews).HasColumnName("total_reviews");
            summary.Property(value => value.Average).HasColumnName("average_rating");

            summary.Ignore(value => value.HasReviews);

            summary.HasIndex(value => value.Average).HasDatabaseName("ix_providers_rating");
        });

        builder.Navigation(provider => provider.ReviewSummary).IsRequired();
    }

    private static void ConfigureVerificationSummary(EntityTypeBuilder<Provider> builder)
    {
        builder.OwnsOne(provider => provider.VerificationSummary, summary =>
        {
            summary.Property(value => value.IsVerified).HasColumnName("is_verified");

            summary.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            summary.Property(value => value.VerifiedKindCount).HasColumnName("verified_kind_count");
            summary.Property(value => value.VerifiedAt).HasColumnName("verified_at");
            summary.Property(value => value.ExpiresAt).HasColumnName("verification_expires_at");

            // The client's "Show only Community Verified providers" toggle, indexed.
            summary.HasIndex(value => value.IsVerified).HasDatabaseName("ix_providers_verified");

            // The sweeper's query: grants that have lapsed but still read as current.
            summary.HasIndex(value => value.ExpiresAt).HasDatabaseName("ix_providers_verification_expiry");
        });

        builder.Navigation(provider => provider.VerificationSummary).IsRequired();
    }

    private static void ConfigureMembers(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Members, member =>
        {
            member.ToTable(
                ProviderPersistenceConstants.ProviderMembersTable, ProvidersModuleMetadata.Schema);

            member.HasKey(value => value.Id);

            member.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderMembershipId>());

            member.Property(value => value.UserId)
                .HasConversion(ProviderValueConverters.Id<UserId>())
                .IsRequired();

            member.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            member.Property(value => value.InvitedBy)
                .HasConversion(ProviderValueConverters.Id<UserId>());

            member.Property(value => value.Title)
                .HasMaxLength(ProviderMembership.MaxTitleLength);

            member.WithOwner().HasForeignKey("ProviderId");

            // The "my providers" query, and the per-request role check on every write.
            member.HasIndex(value => value.UserId).HasDatabaseName("ix_provider_members_user");

            member.HasIndex("ProviderId", "UserId")
                .IsUnique()
                .HasDatabaseName("ux_provider_members_provider_user");

            member.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureServices(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Services, service =>
        {
            service.ToTable(
                ProviderPersistenceConstants.ProviderServicesTable, ProvidersModuleMetadata.Schema);

            service.HasKey(value => value.Id);

            service.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderServiceId>());

            service.Property(value => value.Name)
                .HasMaxLength(ProviderService.MaxNameLength)
                .IsRequired();

            service.Property(value => value.Description)
                .HasMaxLength(ProviderService.MaxDescriptionLength);

            service.Property(value => value.CategoryId)
                .HasConversion(ProviderValueConverters.Id<ServiceCategoryId>());

            service.Property(value => value.DeliveryMode)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            service.Property(value => value.ImageMediaId)
                .HasConversion(ProviderValueConverters.Id<MediaId>());

            ConfigurePricing(service.OwnsOne(value => value.Pricing));
            service.Navigation(value => value.Pricing).IsRequired();

            service.OwnsMany(value => value.Offerings, offering =>
            {
                offering.ToTable(
                    ProviderPersistenceConstants.ServiceOfferingsTable,
                    ProvidersModuleMetadata.Schema);

                offering.HasKey(inner => inner.Id);

                offering.Property(inner => inner.Id)
                    .HasConversion(ProviderValueConverters.Id<ServiceOfferingId>());

                offering.Property(inner => inner.ServiceId)
                    .HasConversion(ProviderValueConverters.Id<ProviderServiceId>())
                    .IsRequired();

                offering.Property(inner => inner.Name)
                    .HasMaxLength(ServiceOffering.MaxNameLength)
                    .IsRequired();

                offering.Property(inner => inner.Description)
                    .HasMaxLength(ServiceOffering.MaxDescriptionLength);

                ConfigurePricing(offering.OwnsOne(inner => inner.Pricing));
                offering.Navigation(inner => inner.Pricing).IsRequired();

                offering.WithOwner().HasForeignKey(inner => inner.ServiceId);
                offering.Ignore(inner => inner.DomainEvents);
            });

            service.WithOwner().HasForeignKey("ProviderId");

            service.HasIndex(value => value.CategoryId).HasDatabaseName("ix_provider_services_category");
            service.HasIndex(value => value.Name).HasDatabaseName("ix_provider_services_name");

            service.Ignore(value => value.DedupeKey);
            service.Ignore(value => value.DomainEvents);
        });

    private static void ConfigurePricing<TOwner>(OwnedNavigationBuilder<TOwner, ProviderPricing> pricing)
        where TOwner : class
    {
        pricing.Property(value => value.Model)
            .HasColumnName("pricing_model")
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        pricing.Property(value => value.MinAmount)
            .HasColumnName("price_min")
            .HasPrecision(
                ProviderPersistenceConstants.MoneyPrecision,
                ProviderPersistenceConstants.MoneyScale);

        pricing.Property(value => value.MaxAmount)
            .HasColumnName("price_max")
            .HasPrecision(
                ProviderPersistenceConstants.MoneyPrecision,
                ProviderPersistenceConstants.MoneyScale);

        pricing.Property(value => value.Currency)
            .HasColumnName("price_currency")
            .HasMaxLength(ProviderPersistenceConstants.CurrencyLength);

        pricing.Property(value => value.Unit)
            .HasColumnName("price_unit")
            .HasMaxLength(ProviderPricing.MaxUnitLength);

        pricing.Property(value => value.IsNegotiable).HasColumnName("price_is_negotiable");

        pricing.Ignore(value => value.HasAmount);
    }

    private static void ConfigureServiceAreas(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.ServiceAreas, area =>
        {
            area.ToTable(
                ProviderPersistenceConstants.ProviderServiceAreasTable,
                ProvidersModuleMetadata.Schema);

            area.HasKey(value => value.Id);

            area.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderServiceAreaId>());

            area.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            area.Property(value => value.CountryCode)
                .HasMaxLength(ProviderPersistenceConstants.CountryCodeLength);

            area.Property(value => value.HierarchyPath)
                .HasMaxLength(ProviderPersistenceConstants.HierarchyPathLength);

            area.Property(value => value.Label).HasMaxLength(ProviderServiceArea.MaxLabelLength);

            // A town service area by name (14 Oct 2026). Nullable: every other kind leaves it empty.
            area.Property(value => value.CityName).HasMaxLength(ProviderPersistenceConstants.NameLength);

            area.Property(value => value.TravelSurcharge)
                .HasPrecision(
                    ProviderPersistenceConstants.MoneyPrecision,
                    ProviderPersistenceConstants.MoneyScale);

            area.Property(value => value.TravelSurchargeCurrency)
                .HasMaxLength(ProviderPersistenceConstants.CurrencyLength);

            area.WithOwner().HasForeignKey("ProviderId");

            // What makes a mobile provider findable outside its own postcode: the prefix scan
            // over declared coverage.
            area.HasIndex(value => value.HierarchyPath)
                .HasDatabaseName("ix_provider_service_areas_path");

            area.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_provider_service_areas_country");

            area.Ignore(value => value.DedupeKey);
            area.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureLocations(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Locations, location =>
        {
            location.ToTable(
                ProviderPersistenceConstants.ProviderLocationsTable,
                ProvidersModuleMetadata.Schema);

            location.HasKey(value => value.Id);

            location.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderLocationId>());

            location.Property(value => value.Name)
                .HasMaxLength(ProviderLocation.MaxNameLength)
                .IsRequired();

            location.Property(value => value.OperatingHoursNote)
                .HasMaxLength(ProviderLocation.MaxNoteLength);

            location.Property(value => value.PhoneNumber)
                .HasMaxLength(ProviderLocation.MaxPhoneLength);

            location.OwnsOne(value => value.Place, place =>
            {
                place.Property(inner => inner.CountryCode)
                    .HasColumnName("country_code")
                    .HasMaxLength(ProviderPersistenceConstants.CountryCodeLength);

                place.Property(inner => inner.AreaId).HasColumnName("area_id");

                place.Property(inner => inner.AreaName)
                    .HasColumnName("area_name")
                    .HasMaxLength(ProviderPersistenceConstants.NameLength);

                place.Property(inner => inner.CityName)
                    .HasColumnName("city_name")
                    .HasMaxLength(ProviderPersistenceConstants.NameLength);

                place.Property(inner => inner.Latitude).HasColumnName("latitude");
                place.Property(inner => inner.Longitude).HasColumnName("longitude");

                place.Property(inner => inner.HierarchyPath)
                    .HasColumnName("hierarchy_path")
                    .HasMaxLength(ProviderPersistenceConstants.HierarchyPathLength);

                place.Property(inner => inner.AddressLine)
                    .HasColumnName("address_line")
                    .HasMaxLength(ProviderPersistenceConstants.LongNameLength);

                place.Property(inner => inner.PostalCode)
                    .HasColumnName("postal_code")
                    .HasMaxLength(32);
            });

            location.Navigation(value => value.Place).IsRequired();

            location.WithOwner().HasForeignKey("ProviderId");
            location.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureLanguages(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Languages, language =>
        {
            language.ToTable(
                ProviderPersistenceConstants.ProviderLanguagesTable,
                ProvidersModuleMetadata.Schema);

            language.HasKey(value => value.Id);

            language.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderLanguageId>());

            language.Property(value => value.Code)
                .HasMaxLength(ProviderPersistenceConstants.LanguageCodeLength)
                .IsRequired();

            language.Property(value => value.Proficiency)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            language.WithOwner().HasForeignKey("ProviderId");

            // "Providers who speak Twi" is a filter, so the code is indexed.
            language.HasIndex(value => value.Code).HasDatabaseName("ix_provider_languages_code");

            language.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureContactMethods(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.ContactMethods, method =>
        {
            method.ToTable(
                ProviderPersistenceConstants.ProviderContactMethodsTable,
                ProvidersModuleMetadata.Schema);

            method.HasKey(value => value.Id);

            method.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderContactMethodId>());

            method.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            method.Property(value => value.Value)
                .HasMaxLength(ProviderContactMethod.MaxValueLength)
                .IsRequired();

            method.Property(value => value.Label)
                .HasMaxLength(ProviderContactMethod.MaxLabelLength);

            method.WithOwner().HasForeignKey("ProviderId");

            method.Ignore(value => value.DedupeKey);
            method.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureGallery(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Gallery, item =>
        {
            item.ToTable(
                ProviderPersistenceConstants.ProviderGalleryTable, ProvidersModuleMetadata.Schema);

            item.HasKey(value => value.Id);

            item.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderGalleryItemId>());

            item.Property(value => value.MediaId)
                .HasConversion(ProviderValueConverters.Id<MediaId>())
                .IsRequired();

            item.Property(value => value.Role)
                .HasMaxLength(ProviderGalleryItem.MaxRoleLength)
                .IsRequired();

            item.Property(value => value.Caption)
                .HasMaxLength(ProviderGalleryItem.MaxCaptionLength);

            item.WithOwner().HasForeignKey("ProviderId");

            // The unclaimed-asset sweep: everything B5 is about to delete out from under us.
            item.HasIndex(value => value.IsClaimed).HasDatabaseName("ix_provider_gallery_claimed");

            item.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureCertifications(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Certifications, certification =>
        {
            certification.ToTable(
                ProviderPersistenceConstants.ProviderCertificationsTable,
                ProvidersModuleMetadata.Schema);

            certification.HasKey(value => value.Id);

            certification.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderCertificationId>());

            certification.Property(value => value.Name)
                .HasMaxLength(ProviderCertification.MaxNameLength)
                .IsRequired();

            certification.Property(value => value.Issuer)
                .HasMaxLength(ProviderCertification.MaxIssuerLength)
                .IsRequired();

            certification.Property(value => value.Reference)
                .HasMaxLength(ProviderCertification.MaxReferenceLength);

            certification.Property(value => value.IssuedOn)
                .HasConversion(ProviderValueConverters.NullableDate);

            certification.Property(value => value.ExpiresOn)
                .HasConversion(ProviderValueConverters.NullableDate);

            certification.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            certification.Property(value => value.EvidenceMediaId)
                .HasConversion(ProviderValueConverters.Id<MediaId>());

            certification.WithOwner().HasForeignKey("ProviderId");

            // The expiry sweep, and the "certified only" filter.
            certification.HasIndex(value => value.ExpiresOn)
                .HasDatabaseName("ix_provider_certifications_expiry");

            certification.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureTags(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Tags, tag =>
        {
            tag.ToTable(
                ProviderPersistenceConstants.ProviderTagsTable, ProvidersModuleMetadata.Schema);

            tag.HasKey(value => value.Id);

            tag.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderTagId>());

            tag.Property(value => value.Label)
                .HasMaxLength(Domain.Policies.ProviderLimits.MaxTagLength)
                .IsRequired();

            tag.Property(value => value.Slug)
                .HasMaxLength(Domain.Policies.ProviderLimits.MaxTagLength)
                .IsRequired();

            tag.Property(value => value.Source).HasMaxLength(64);

            tag.WithOwner().HasForeignKey("ProviderId");

            // "halal" is a query members type. Indexed on the slug, never the label.
            tag.HasIndex(value => value.Slug).HasDatabaseName("ix_provider_tags_slug");

            tag.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureOperatingHours(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.OperatingHours, hours =>
        {
            hours.ToTable(
                ProviderPersistenceConstants.ProviderOperatingHoursTable,
                ProvidersModuleMetadata.Schema);

            hours.HasKey(value => value.Id);

            hours.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<OperatingHoursId>());

            hours.Property(value => value.Day)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            hours.OwnsMany(value => value.Windows, window =>
            {
                window.ToTable(
                    ProviderPersistenceConstants.ProviderAvailabilityWindowsTable,
                    ProvidersModuleMetadata.Schema);

                window.HasKey(inner => inner.Id);

                window.Property(inner => inner.Id)
                    .HasConversion(ProviderValueConverters.Id<AvailabilityWindowId>());

                window.Property(inner => inner.OperatingHoursId)
                    .HasConversion(ProviderValueConverters.Id<OperatingHoursId>())
                    .IsRequired();

                window.Property(inner => inner.OpensAt)
                    .HasConversion(ProviderValueConverters.TimeOfDay)
                    .IsRequired();

                window.Property(inner => inner.ClosesAt)
                    .HasConversion(ProviderValueConverters.TimeOfDay)
                    .IsRequired();

                window.Property(inner => inner.Label).HasMaxLength(64);

                window.WithOwner().HasForeignKey(inner => inner.OperatingHoursId);

                window.Ignore(inner => inner.CrossesMidnight);
                window.Ignore(inner => inner.Duration);
                window.Ignore(inner => inner.DomainEvents);
            });

            hours.WithOwner().HasForeignKey("ProviderId");

            hours.Ignore(value => value.IsSpecified);
            hours.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureAvailabilityExceptions(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.AvailabilityExceptions, exception =>
        {
            exception.ToTable(
                ProviderPersistenceConstants.ProviderAvailabilityExceptionsTable,
                ProvidersModuleMetadata.Schema);

            exception.HasKey(value => value.Id);

            exception.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<AvailabilityExceptionId>());

            exception.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            exception.Property(value => value.StartsOn)
                .HasConversion(ProviderValueConverters.Date)
                .IsRequired();

            exception.Property(value => value.EndsOn)
                .HasConversion(ProviderValueConverters.Date)
                .IsRequired();

            exception.Property(value => value.OpensAt)
                .HasConversion(ProviderValueConverters.TimeOfDay);

            exception.Property(value => value.ClosesAt)
                .HasConversion(ProviderValueConverters.TimeOfDay);

            exception.Property(value => value.Reason)
                .HasMaxLength(AvailabilityException.MaxReasonLength);

            exception.WithOwner().HasForeignKey("ProviderId");

            // The "does an exception apply today" lookup, and the lapsed-exception sweep.
            exception.HasIndex(value => value.EndsOn)
                .HasDatabaseName("ix_provider_availability_exceptions_end");

            exception.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureVerifications(EntityTypeBuilder<Provider> builder) =>
        builder.OwnsMany(provider => provider.Verifications, verification =>
        {
            verification.ToTable(
                ProviderPersistenceConstants.ProviderVerificationsTable,
                ProvidersModuleMetadata.Schema);

            verification.HasKey(value => value.Id);

            verification.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ProviderVerificationId>());

            verification.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            verification.Property(value => value.State)
                .HasConversion<string>()
                .HasMaxLength(ProviderPersistenceConstants.EnumLength)
                .IsRequired();

            verification.Property(value => value.SubmittedBy)
                .HasConversion(ProviderValueConverters.Id<UserId>());

            verification.Property(value => value.CertificationId)
                .HasConversion(ProviderValueConverters.Id<ProviderCertificationId>());

            verification.Property(value => value.ReasonCode).HasMaxLength(64);

            verification.Property(value => value.Reason)
                .HasMaxLength(ProviderVerification.MaxReasonLength);

            verification.WithOwner().HasForeignKey("ProviderId");

            // How a Trust integration event finds the claim it decided, without B7 having to
            // have seen the submission.
            verification.HasIndex(value => value.TrustVerificationId)
                .HasDatabaseName("ix_provider_verifications_trust_id");

            verification.Ignore(value => value.IsAwaitingDecision);
            verification.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureIndexes(EntityTypeBuilder<Provider> builder)
    {
        // The pair every discovery query filters and orders on.
        builder.HasIndex(provider => new { provider.Status, provider.PublishedAt })
            .HasDatabaseName("ix_providers_status_published");

        builder.HasIndex(provider => provider.CategoryId)
            .HasDatabaseName("ix_providers_category");

        builder.HasIndex(provider => provider.Type)
            .HasDatabaseName("ix_providers_type");

        builder.HasIndex(provider => provider.IsFeatured)
            .HasDatabaseName("ix_providers_featured");

        // Soft-deleted rows are excluded by a global query filter, so the column is in every
        // predicate the module ever issues and earns its own index.
        builder.HasIndex(provider => provider.IsDeleted)
            .HasDatabaseName("ix_providers_deleted");
    }
}

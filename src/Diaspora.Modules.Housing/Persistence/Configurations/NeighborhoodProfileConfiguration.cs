using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the neighbourhood profile and its languages — two tables.
/// <para>
/// <b>The unique index on the area is the whole ownership boundary made physical.</b> One profile
/// per B4 area, enforced by the database: a second row for Peckham would be a second opinion about
/// one place with no way to say which is the platform's, and it is precisely the duplication this
/// module was designed to avoid. It is also why the profile is found by area identifier rather than
/// by its own — callers know which place they mean.
/// </para>
/// <para>
/// The geographic columns here are all copies of B4's values, cached so a profile card needs no
/// cross-module join. B4 wins on every disagreement.
/// </para>
/// </summary>
public sealed class NeighborhoodProfileConfiguration
    : IEntityTypeConfiguration<NeighborhoodProfile>
{
    public void Configure(EntityTypeBuilder<NeighborhoodProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.NeighborhoodProfilesTable,
            HousingModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        // The optimistic concurrency token, and it has to be declared here rather than merely
        // bumped in the aggregate. EnsureRevision rejects a stale editor that sends back the
        // revision it loaded; nothing stops the editor that does not send one, and two saves from
        // one landlord's two devices would then both commit with the later one silently winning.
        // Declaring the token makes the database arbitrate, which is the only place it can be
        // settled. B10 learnt this; B11 shipped without it until an integration test raced two
        // contexts and found no exception at all.
        builder.Property(profile => profile.Revision).IsConcurrencyToken();

        builder.Property(profile => profile.Id)
            .HasConversion(HousingValueConverters.Id<NeighborhoodProfileId>());

        builder.Property(profile => profile.AreaId).IsRequired();

        builder.Property(profile => profile.CountryCode)
            .HasMaxLength(HousingPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(profile => profile.Name)
            .HasMaxLength(HousingLimits.MaxLocationName)
            .IsRequired();

        builder.Property(profile => profile.Slug)
            .HasMaxLength(HousingLimits.MaxSlug)
            .IsRequired();

        builder.Property(profile => profile.CityName)
            .HasMaxLength(HousingLimits.MaxLocationName);

        builder.Property(profile => profile.RegionName)
            .HasMaxLength(HousingLimits.MaxLocationName);

        builder.Property(profile => profile.AreaPath).HasMaxLength(HousingLimits.MaxAreaPath);

        builder.Property(profile => profile.LastReviewedBy)
            .HasConversion(HousingValueConverters.Id<UserId>());

        ConfigureNarrative(builder);
        ConfigureLivingIndex(builder);
        ConfigureLanguages(builder);

        // One profile per place. The ownership boundary, enforced by the database.
        builder.HasIndex(profile => profile.AreaId)
            .IsUnique()
            .HasDatabaseName("ix_housing_neighborhoods_area");

        // The client addresses neighbourhoods by slug, and a slug is unique within its country
        // rather than globally — there is a Springfield in twelve states, which is B4's own note.
        builder.HasIndex(profile => new { profile.CountryCode, profile.Slug })
            .HasDatabaseName("ix_housing_neighborhoods_country_slug");

        // The subtree query: every neighbourhood under a city.
        builder.HasIndex(profile => profile.AreaPath)
            .HasDatabaseName("ix_housing_neighborhoods_area_path");

        // The discovery ordering: the places with the most to look at, first.
        builder.HasIndex(profile => profile.ListingCount)
            .HasDatabaseName("ix_housing_neighborhoods_listing_count");

        builder.Ignore(profile => profile.DomainEvents);
        builder.Ignore(profile => profile.Completeness);
        builder.Ignore(profile => profile.HasContent);
    }

    private static void ConfigureNarrative(EntityTypeBuilder<NeighborhoodProfile> builder)
    {
        builder.OwnsOne(profile => profile.Narrative, narrative =>
        {
            narrative.Property(value => value.Description)
                .HasColumnName("description")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Property(value => value.Overview)
                .HasColumnName("overview")
                .HasMaxLength(HousingLimits.MaxNeighborhoodOverview);

            narrative.Property(value => value.SafetySummary)
                .HasColumnName("safety_summary")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Property(value => value.TransportSummary)
                .HasColumnName("transport_summary")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Property(value => value.CostOfLivingSummary)
                .HasColumnName("cost_of_living_summary")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Property(value => value.CommunitySummary)
                .HasColumnName("community_summary")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Property(value => value.ClimateNotes)
                .HasColumnName("climate_notes")
                .HasMaxLength(HousingLimits.MaxNeighborhoodSummary);

            narrative.Ignore(value => value.IsEmpty);
            narrative.Ignore(value => value.WrittenCount);
        });

        builder.Navigation(profile => profile.Narrative).IsRequired();
    }

    private static void ConfigureLivingIndex(EntityTypeBuilder<NeighborhoodProfile> builder)
    {
        builder.OwnsOne(profile => profile.LivingIndex, index =>
        {
            index.Property(value => value.Walkability)
                .HasColumnName("walkability")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.FamilyFriendliness)
                .HasColumnName("family_friendliness")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.StudentFriendliness)
                .HasColumnName("student_friendliness")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.Nightlife)
                .HasColumnName("nightlife")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.NoiseLevel)
                .HasColumnName("noise_level")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.Accessibility)
                .HasColumnName("accessibility")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.ParkingAvailability)
                .HasColumnName("parking_availability")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.CostOfLiving)
                .HasColumnName("cost_of_living")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Property(value => value.InternetAvailability)
                .HasColumnName("internet_availability")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            index.Ignore(value => value.Assessed);
            index.Ignore(value => value.AssessedCount);
        });

        builder.Navigation(profile => profile.LivingIndex).IsRequired();
    }

    private static void ConfigureLanguages(EntityTypeBuilder<NeighborhoodProfile> builder) =>
        builder.OwnsMany(profile => profile.PopularLanguages, language =>
        {
            language.ToTable(
                HousingPersistenceConstants.NeighborhoodLanguagesTable,
                HousingModuleMetadata.Schema);

            // The language name is the identity, so it is the key. Left to convention EF invents a
            // store-generated shadow integer inside a composite primary key, which SQLite cannot
            // fill and refuses the insert for — see HousingListingConfiguration for the full note.
            language.WithOwner().HasForeignKey("NeighborhoodProfileId");
            language.HasKey("NeighborhoodProfileId", nameof(NeighborhoodLanguage.Name));

            language.Property(value => value.Name)
                .HasMaxLength(HousingLimits.MaxLanguageName)
                .IsRequired();

            language.Property(value => value.Prominence)
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            // Declared inside the OwnsMany block: on the outer builder this would target the
            // profiles table and index a column that is not there.
            language.HasIndex(value => value.Name)
                .HasDatabaseName("ix_housing_neighborhood_languages_name");
        });
}

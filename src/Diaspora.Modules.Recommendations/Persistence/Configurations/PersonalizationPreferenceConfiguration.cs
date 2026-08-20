using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps what the member actually asked for.
/// <para>
/// <b>Not soft-deletable, and that is the deliberate difference from the candidate and the set.</b>
/// A preference record is meaningless once its member is gone, and a soft-deleted one would leave a
/// list of the categories somebody muted sitting behind a query filter — which is a record of their
/// opinions kept after they asked to be forgotten.
/// </para>
/// <para>
/// Four owned collections, each with a declared caller-supplied key and its uniqueness index inside
/// its own block. See <see cref="PersonalizationProfileConfiguration"/> for why both are stated
/// rather than left to convention.
/// </para>
/// </summary>
public sealed class PersonalizationPreferenceConfiguration
    : IEntityTypeConfiguration<PersonalizationPreference>
{
    public void Configure(EntityTypeBuilder<PersonalizationPreference> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.PreferencesTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(preference => preference.Id);

        builder.Property(preference => preference.Id)
            .HasConversion(RecommendationValueConverters.Id<PersonalizationPreferenceId>());

        builder.Property(preference => preference.UserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(preference => preference.Revision).IsConcurrencyToken();

        builder.Property(preference => preference.PreferredLanguage)
            .HasMaxLength(RecommendationPersistenceConstants.LanguageLength);

        builder.Property(preference => preference.PreferredCountryCode)
            .HasMaxLength(RecommendationPersistenceConstants.CountryLength);

        builder.OwnsMany(preference => preference.Categories, category =>
        {
            category.ToTable(
                RecommendationPersistenceConstants.PreferenceCategoriesTable,
                RecommendationsModuleMetadata.Schema);

            category.HasKey(value => value.PreferenceId);
            category.Property(value => value.PreferenceId).ValueGeneratedNever();

            category.Property(value => value.CategoryKey)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            category.Property(value => value.TargetType)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

            category.HasIndex(
                    "PersonalizationPreferenceId",
                    nameof(CategoryPreference.CategoryKey),
                    nameof(CategoryPreference.TargetType))
                .IsUnique()
                .HasDatabaseName("ix_preference_categories_unique");
        });

        builder.OwnsMany(preference => preference.BlockedTargets, block =>
        {
            block.ToTable(
                RecommendationPersistenceConstants.PreferenceBlocksTable,
                RecommendationsModuleMetadata.Schema);

            block.HasKey(value => value.BlockId);
            block.Property(value => value.BlockId).ValueGeneratedNever();

            block.Property(value => value.TargetType)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            block.HasIndex(
                    "PersonalizationPreferenceId",
                    nameof(BlockedRecommendationTarget.TargetType),
                    nameof(BlockedRecommendationTarget.TargetId))
                .IsUnique()
                .HasDatabaseName("ix_preference_blocks_unique");
        });

        builder.OwnsMany(preference => preference.Surfaces, surface =>
        {
            surface.ToTable(
                RecommendationPersistenceConstants.PreferenceSurfacesTable,
                RecommendationsModuleMetadata.Schema);

            surface.HasKey(value => value.PreferenceId);
            surface.Property(value => value.PreferenceId).ValueGeneratedNever();

            surface.Property(value => value.Surface)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            surface.HasIndex("PersonalizationPreferenceId", nameof(SurfacePreference.Surface))
                .IsUnique()
                .HasDatabaseName("ix_preference_surfaces_unique");
        });

        builder.OwnsMany(preference => preference.FavoriteLocations, location =>
        {
            location.ToTable(
                RecommendationPersistenceConstants.PreferenceLocationsTable,
                RecommendationsModuleMetadata.Schema);

            location.HasKey(value => value.LocationId);
            location.Property(value => value.LocationId).ValueGeneratedNever();

            location.Property(value => value.Scope)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            location.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength)
                .IsRequired();

            location.Property(value => value.DisplayName)
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

            location.HasIndex(
                    "PersonalizationPreferenceId",
                    nameof(FavoriteLocation.Scope),
                    nameof(FavoriteLocation.Key))
                .IsUnique()
                .HasDatabaseName("ix_preference_locations_unique");
        });

        builder.HasIndex(preference => preference.UserId)
            .IsUnique()
            .HasDatabaseName("ix_personalization_preferences_user");

        builder.Ignore(preference => preference.DomainEvents);
    }
}

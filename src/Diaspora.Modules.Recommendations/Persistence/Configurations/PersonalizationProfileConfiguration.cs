using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the derived profile and its three owned collections.
/// <para>
/// <b>One row per member, and the unique index is what makes that true.</b> Two profiles for one
/// person would mean recommendations that changed depending on which one a query returned — and the
/// race that creates them, two consumers folding signals for a member with no profile yet, is the
/// normal case rather than the exception.
/// </para>
/// <para>
/// <b>Every owned collection declares its key and marks it caller-supplied.</b> Without
/// <c>HasKey</c>, EF invents a store-generated shadow integer inside a composite primary key and
/// SQLite refuses the insert once a row exists; without <c>ValueGeneratedNever</c>, EF emits an
/// UPDATE where an INSERT was meant and it surfaces as a bogus concurrency conflict. This platform
/// has paid for both.
/// </para>
/// <para>
/// <b>The indexes over the owned collections are declared inside their own blocks.</b> An index on an
/// owned type declared on the parent builder silently does not exist, which is the sort of defect
/// that shows up as a slow query six months later with nothing to point at.
/// </para>
/// </summary>
public sealed class PersonalizationProfileConfiguration : IEntityTypeConfiguration<PersonalizationProfile>
{
    public void Configure(EntityTypeBuilder<PersonalizationProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.ProfilesTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(RecommendationValueConverters.Id<PersonalizationProfileId>());

        builder.Property(profile => profile.UserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(profile => profile.Revision).IsConcurrencyToken();

        builder.Property(profile => profile.PreferredLanguage)
            .HasMaxLength(RecommendationPersistenceConstants.LanguageLength);

        builder.Property(profile => profile.PrimaryCountryCode)
            .HasMaxLength(RecommendationPersistenceConstants.CountryLength);

        builder.Property(profile => profile.PrimaryCityName)
            .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

        builder.Property(profile => profile.PrimaryNeighborhoodName)
            .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

        builder.OwnsMany(profile => profile.Interests, interest =>
        {
            interest.ToTable(
                RecommendationPersistenceConstants.ProfileInterestsTable,
                RecommendationsModuleMetadata.Schema);

            interest.HasKey(value => value.InterestId);
            interest.Property(value => value.InterestId).ValueGeneratedNever();

            interest.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            interest.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            interest.Property(value => value.Affinity)
                .HasPrecision(
                    RecommendationPersistenceConstants.ScorePrecision,
                    RecommendationPersistenceConstants.ScoreScale);

            // Declared inside the block. See the class summary — outside it does not exist.
            interest.HasIndex(
                    "PersonalizationProfileId",
                    nameof(ProfileInterest.Kind),
                    nameof(ProfileInterest.Key))
                .IsUnique()
                .HasDatabaseName("ix_profile_interests_unique");
        });

        builder.OwnsMany(profile => profile.LocationAffinities, affinity =>
        {
            affinity.ToTable(
                RecommendationPersistenceConstants.ProfileLocationsTable,
                RecommendationsModuleMetadata.Schema);

            affinity.HasKey(value => value.AffinityId);
            affinity.Property(value => value.AffinityId).ValueGeneratedNever();

            affinity.Property(value => value.Scope)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            affinity.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength)
                .IsRequired();

            affinity.Property(value => value.DisplayName)
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

            affinity.Property(value => value.Affinity)
                .HasPrecision(
                    RecommendationPersistenceConstants.ScorePrecision,
                    RecommendationPersistenceConstants.ScoreScale);

            affinity.HasIndex(
                    "PersonalizationProfileId",
                    nameof(ProfileLocationAffinity.Scope),
                    nameof(ProfileLocationAffinity.Key))
                .IsUnique()
                .HasDatabaseName("ix_profile_locations_unique");
        });

        builder.OwnsMany(profile => profile.ActivitySummaries, summary =>
        {
            summary.ToTable(
                RecommendationPersistenceConstants.ProfileActivityTable,
                RecommendationsModuleMetadata.Schema);

            summary.HasKey(value => value.SummaryId);
            summary.Property(value => value.SummaryId).ValueGeneratedNever();

            summary.Property(value => value.TargetType)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            summary.HasIndex("PersonalizationProfileId", nameof(ProfileActivitySummary.TargetType))
                .IsUnique()
                .HasDatabaseName("ix_profile_activity_unique");
        });

        builder.HasIndex(profile => profile.UserId)
            .IsUnique()
            .HasDatabaseName("ix_personalization_profiles_user");

        // The staleness sweep picks the least recently built. Without this it sorts the whole table.
        builder.HasIndex(profile => profile.LastBuiltAt)
            .HasDatabaseName("ix_personalization_profiles_stale");

        builder.Ignore(profile => profile.DomainEvents);
    }
}

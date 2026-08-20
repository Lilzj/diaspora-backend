using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the candidate projection.
/// <para>
/// <b>The table every generation reads and every consumer writes, so its indexes are the module's
/// performance budget.</b> The retrieval index — active, type, country, popularity — is the one the
/// pool query plans against, and it is composite in that order because the planner narrows on
/// equality before it sorts.
/// </para>
/// <para>
/// <b>The target reference is owned rather than two loose columns, and the unique index is on the
/// pair.</b> A job and a housing listing may in principle share a <see cref="Guid"/>; an index on the
/// identifier alone would refuse the second one to arrive, and the failure would look like a bug in
/// whichever vertical happened to publish second.
/// </para>
/// <para>
/// <b>Soft-deletable, so the shared context applies the global query filter and
/// <c>SoftDeleteConverter</c> repairs the owned graph on delete.</b> Nothing here declares either:
/// B1.0.1 exists so a module never has to.
/// </para>
/// </summary>
public sealed class RecommendationCandidateConfiguration
    : IEntityTypeConfiguration<RecommendationCandidate>
{
    public void Configure(EntityTypeBuilder<RecommendationCandidate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.CandidatesTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .HasConversion(RecommendationValueConverters.Id<RecommendationCandidateId>());

        builder.Property(candidate => candidate.OwnerUserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>());

        builder.Property(candidate => candidate.SourceModule)
            .HasMaxLength(RecommendationPersistenceConstants.ModuleLength)
            .IsRequired();

        builder.Property(candidate => candidate.Title)
            .HasMaxLength(RecommendationPersistenceConstants.TitleLength)
            .IsRequired();

        builder.Property(candidate => candidate.Summary)
            .HasMaxLength(RecommendationPersistenceConstants.SummaryLength);

        builder.Property(candidate => candidate.Slug)
            .HasMaxLength(RecommendationPersistenceConstants.SlugLength);

        builder.Property(candidate => candidate.MediaReference)
            .HasMaxLength(RecommendationPersistenceConstants.MediaReferenceLength);

        builder.Property(candidate => candidate.CategoryKey)
            .HasMaxLength(RecommendationPersistenceConstants.KeyLength);

        builder.Property(candidate => candidate.CountryCode)
            .HasMaxLength(RecommendationPersistenceConstants.CountryLength);

        builder.Property(candidate => candidate.RegionName)
            .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

        builder.Property(candidate => candidate.CityName)
            .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

        builder.Property(candidate => candidate.NeighborhoodName)
            .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

        builder.Property(candidate => candidate.LanguageCode)
            .HasMaxLength(RecommendationPersistenceConstants.LanguageLength);

        builder.Property(candidate => candidate.Visibility)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(candidate => candidate.PopularityScore)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.Property(candidate => candidate.QualityScore)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        // The paid-placement weight, on the same precision as every other score so a promotion and an
        // organic score are comparable numbers rather than two scales that happen to overlap.
        builder.Property(candidate => candidate.PromotionWeight)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.OwnsOne(candidate => candidate.Target, target =>
        {
            target.Property(value => value.TargetType)
                .HasColumnName("target_type")
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            target.Property(value => value.TargetId)
                .HasColumnName("target_id")
                .IsRequired();

            // On the pair, never on the identifier alone. See the class summary.
            target.HasIndex(
                    nameof(TargetReference.TargetType), nameof(TargetReference.TargetId))
                .IsUnique()
                .HasDatabaseName("ix_recommendation_candidates_target");
        });

        builder.Navigation(candidate => candidate.Target).IsRequired();

        builder.OwnsMany(candidate => candidate.Attributes, attribute =>
        {
            attribute.ToTable(
                RecommendationPersistenceConstants.CandidateAttributesTable,
                RecommendationsModuleMetadata.Schema);

            attribute.HasKey(value => value.AttributeId);
            attribute.Property(value => value.AttributeId).ValueGeneratedNever();

            attribute.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            attribute.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            attribute.HasIndex(
                    "RecommendationCandidateId",
                    nameof(CandidateAttribute.Kind),
                    nameof(CandidateAttribute.Key))
                .IsUnique()
                .HasDatabaseName("ix_candidate_attributes_unique");
        });

        // The pool query. Composite in this order because the planner narrows on equality before it
        // sorts — active and country are equality predicates, popularity is the sort.
        builder.HasIndex(candidate => new
            {
                candidate.IsActive,
                candidate.CountryCode,
                candidate.PopularityScore
            })
            .HasDatabaseName("ix_recommendation_candidates_pool");

        builder.HasIndex(candidate => new { candidate.IsActive, candidate.PublishedAt })
            .HasDatabaseName("ix_recommendation_candidates_recency");

        builder.HasIndex(candidate => candidate.CategoryKey)
            .HasDatabaseName("ix_recommendation_candidates_category");

        // The self-authored policy's read.
        builder.HasIndex(candidate => candidate.OwnerUserId)
            .HasDatabaseName("ix_recommendation_candidates_owner");

        // The pruning sweep.
        builder.HasIndex(candidate => new { candidate.IsActive, candidate.ModifiedAt })
            .HasDatabaseName("ix_recommendation_candidates_withdrawn");

        builder.Ignore(candidate => candidate.DomainEvents);
    }
}

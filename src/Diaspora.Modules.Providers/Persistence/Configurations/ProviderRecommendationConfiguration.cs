using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>
/// Maps provider recommendations, endorsements and testimonials.
/// <para>
/// One member, one recommendation per provider — the same database-enforced invariant as
/// reviews, and for the same reason. A member who has recommended and wants to endorse instead
/// revises the existing record rather than accumulating three.
/// </para>
/// </summary>
public sealed class ProviderRecommendationConfiguration
    : IEntityTypeConfiguration<ProviderRecommendation>
{
    public void Configure(EntityTypeBuilder<ProviderRecommendation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ProviderPersistenceConstants.ProviderRecommendationsTable,
            ProvidersModuleMetadata.Schema);

        builder.HasKey(recommendation => recommendation.Id);

        builder.Property(recommendation => recommendation.Id)
            .HasConversion(ProviderValueConverters.Id<ProviderRecommendationId>());

        builder.Property(recommendation => recommendation.ProviderId)
            .HasConversion(ProviderValueConverters.Id<ProviderId>())
            .IsRequired();

        builder.Property(recommendation => recommendation.RecommenderId)
            .HasConversion(ProviderValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(recommendation => recommendation.Kind)
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(recommendation => recommendation.Note)
            .HasMaxLength(ProviderLimits.MaxRecommendationNoteLength);

        builder.Property(recommendation => recommendation.Skill)
            .HasMaxLength(ProviderRecommendation.MaxSkillLength);

        builder.HasIndex(recommendation => new
        {
            recommendation.ProviderId,
            recommendation.RecommenderId,
            recommendation.IsDeleted
        })
            .IsUnique()
            .HasDatabaseName("ux_provider_recommendations_provider_recommender");

        // The profile's testimonial strip: featured first, newest first.
        builder.HasIndex(recommendation => new
        {
            recommendation.ProviderId,
            recommendation.IsFeatured,
            recommendation.CreatedAt
        })
            .HasDatabaseName("ix_provider_recommendations_provider_featured");

        builder.Ignore(recommendation => recommendation.DomainEvents);
        builder.Ignore(recommendation => recommendation.IsActive);
    }
}

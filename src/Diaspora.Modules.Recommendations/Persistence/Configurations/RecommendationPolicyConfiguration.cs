using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the policy catalogue.
/// <para>
/// Reference data: a handful of rows, read on every generation through the cache, written by an
/// operator perhaps twice a year. The unique index on the key is what makes the catalogue's
/// duplicate refusal enforceable at the database rather than only at the seeder.
/// </para>
/// </summary>
public sealed class RecommendationPolicyConfiguration : IEntityTypeConfiguration<RecommendationPolicy>
{
    public void Configure(EntityTypeBuilder<RecommendationPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.PoliciesTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id)
            .HasConversion(RecommendationValueConverters.Id<RecommendationPolicyId>());

        builder.Property(policy => policy.Revision).IsConcurrencyToken();

        builder.Property(policy => policy.Key)
            .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(policy => policy.Kind)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(policy => policy.Effect)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(policy => policy.Description)
            .HasMaxLength(RecommendationPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(policy => policy.DemotionFactor)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.Property(policy => policy.Surfaces)
            .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);

        builder.Property(policy => policy.TargetTypes)
            .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);

        builder.OwnsMany(policy => policy.Parameters, parameter =>
        {
            parameter.ToTable(
                RecommendationPersistenceConstants.PolicyParametersTable,
                RecommendationsModuleMetadata.Schema);

            parameter.HasKey(value => value.ParameterId);
            parameter.Property(value => value.ParameterId).ValueGeneratedNever();

            parameter.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            parameter.Property(value => value.Value)
                .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);

            parameter.HasIndex("RecommendationPolicyId", nameof(ConfigurationParameter.Key))
                .IsUnique()
                .HasDatabaseName("ix_policy_parameters_unique");
        });

        builder.HasIndex(policy => policy.Key)
            .IsUnique()
            .HasDatabaseName("ix_recommendation_policies_key");

        builder.HasIndex(policy => new { policy.IsEnabled, policy.Order })
            .HasDatabaseName("ix_recommendation_policies_enabled");

        builder.Ignore(policy => policy.DomainEvents);
    }
}

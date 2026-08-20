using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the ranking-strategy catalogue.
/// <para>
/// <b>Unique on key <em>and</em> surface, never on key alone.</b> One implementation is configured on
/// several surfaces with different weights — that is the entire reason weights are data — so a
/// unique index on the key would allow exactly one surface to use each strategy and the second
/// surface's seed would fail at startup.
/// </para>
/// </summary>
public sealed class RankingStrategyConfiguration : IEntityTypeConfiguration<RankingStrategy>
{
    public void Configure(EntityTypeBuilder<RankingStrategy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.StrategiesTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(strategy => strategy.Id);

        builder.Property(strategy => strategy.Id)
            .HasConversion(RecommendationValueConverters.Id<RankingStrategyId>());

        builder.Property(strategy => strategy.Revision).IsConcurrencyToken();

        builder.Property(strategy => strategy.Key)
            .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(strategy => strategy.Kind)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(strategy => strategy.Surface)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(strategy => strategy.TargetType)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(strategy => strategy.Description)
            .HasMaxLength(RecommendationPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(strategy => strategy.Weight)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.Property(strategy => strategy.MinimumScore)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.OwnsMany(strategy => strategy.Parameters, parameter =>
        {
            parameter.ToTable(
                RecommendationPersistenceConstants.StrategyParametersTable,
                RecommendationsModuleMetadata.Schema);

            parameter.HasKey(value => value.ParameterId);
            parameter.Property(value => value.ParameterId).ValueGeneratedNever();

            parameter.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            parameter.Property(value => value.Value)
                .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);

            parameter.HasIndex("RankingStrategyId", nameof(ConfigurationParameter.Key))
                .IsUnique()
                .HasDatabaseName("ix_strategy_parameters_unique");
        });

        // Key and surface. See the class summary — key alone would allow one surface per strategy.
        builder.HasIndex(strategy => new { strategy.Key, strategy.Surface })
            .IsUnique()
            .HasDatabaseName("ix_ranking_strategies_key_surface");

        builder.HasIndex(strategy => new { strategy.Surface, strategy.IsEnabled, strategy.Order })
            .HasDatabaseName("ix_ranking_strategies_surface");

        builder.Ignore(strategy => strategy.DomainEvents);
    }
}

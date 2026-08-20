using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the analytics rollup.
/// <para>
/// <b>Unique on day and surface, which is what makes the rollup idempotent.</b> The sweep runs
/// hourly and folds whatever feedback has arrived since; a second row for the same day would double
/// every figure on it, and the failure would be invisible because both rows would look plausible.
/// </para>
/// <para>
/// <b>Not soft-deletable.</b> A rollup is a measurement that outlives what it measured — the sets and
/// feedback rows behind a month-old figure are long swept — and a soft delete would hide a
/// measurement rather than remove it.
/// </para>
/// </summary>
public sealed class RecommendationAnalyticsConfiguration
    : IEntityTypeConfiguration<RecommendationAnalytics>
{
    public void Configure(EntityTypeBuilder<RecommendationAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.AnalyticsTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(RecommendationValueConverters.Id<RecommendationAnalyticsId>());

        builder.Property(analytics => analytics.Surface)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsMany(analytics => analytics.Breakdowns, breakdown =>
        {
            breakdown.ToTable(
                RecommendationPersistenceConstants.AnalyticsBreakdownsTable,
                RecommendationsModuleMetadata.Schema);

            breakdown.HasKey(value => value.BreakdownId);
            breakdown.Property(value => value.BreakdownId).ValueGeneratedNever();

            breakdown.Property(value => value.Dimension)
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            breakdown.Property(value => value.Key)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            breakdown.HasIndex(
                    "RecommendationAnalyticsId",
                    nameof(AnalyticsBreakdown.Dimension),
                    nameof(AnalyticsBreakdown.Key))
                .IsUnique()
                .HasDatabaseName("ix_analytics_breakdowns_unique");
        });

        // What makes the hourly fold idempotent. See the class summary.
        builder.HasIndex(analytics => new { analytics.Day, analytics.Surface })
            .IsUnique()
            .HasDatabaseName("ix_recommendation_analytics_day");

        builder.Ignore(analytics => analytics.DomainEvents);
    }
}

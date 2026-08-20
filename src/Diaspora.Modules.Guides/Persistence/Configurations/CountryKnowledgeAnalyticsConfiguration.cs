using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the per-country usage counters and their category breakdown.
/// <para>
/// <b>No concurrency token, and that is the right call for a counter.</b> Every other aggregate in
/// this phase carries one because a lost curation edit is a person's work silently discarded. A lost
/// increment is one view among thousands, and a token here would turn the platform's most-read
/// screen into a source of concurrency conflicts under exactly the load that makes the number worth
/// having. Counters are the one place where "roughly right and always available" beats "exact and
/// occasionally refused".
/// </para>
/// <para>
/// The derived rates are computed properties and are ignored: storing a ratio is storing a number
/// that can disagree with the two it was derived from.
/// </para>
/// </summary>
public sealed class CountryKnowledgeAnalyticsConfiguration
    : IEntityTypeConfiguration<CountryKnowledgeAnalytics>
{
    public void Configure(EntityTypeBuilder<CountryKnowledgeAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            KnowledgePersistenceConstants.CountryAnalyticsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(GuideValueConverters.Id<CountryKnowledgeAnalyticsId>());

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(KnowledgePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.HasIndex(analytics => analytics.CountryCode)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_country_analytics_country");

        // The explore grid's default ordering: the countries people are actually asking about.
        builder.HasIndex(analytics => analytics.DirectoryViews)
            .HasDatabaseName("ix_knowledge_country_analytics_views");

        ConfigureCategoryViews(builder);

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.DiscoveryRate);
        builder.Ignore(analytics => analytics.ClickThroughRate);
        builder.Ignore(analytics => analytics.CompletionRate);
        builder.Ignore(analytics => analytics.PathCompletionRate);
    }

    private static void ConfigureCategoryViews(
        EntityTypeBuilder<CountryKnowledgeAnalytics> builder) =>
        builder.OwnsMany(analytics => analytics.CategoryBreakdown, view =>
        {
            view.ToTable(
                KnowledgePersistenceConstants.CategoryViewsTable, GuidesModuleMetadata.Schema);

            view.Property(value => value.KnowledgeCategoryKey)
                .HasMaxLength(KnowledgeLimits.MaxCategoryKey)
                .IsRequired();

            view.WithOwner().HasForeignKey("CountryKnowledgeAnalyticsId");

            view.HasKey(
                "CountryKnowledgeAnalyticsId",
                nameof(KnowledgeCategoryViewCount.KnowledgeCategoryKey));
        });
}

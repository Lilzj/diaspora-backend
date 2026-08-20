using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the per-guide analytics counters.
/// <para>
/// <b>The unique index on the guide is the get-or-create arbiter.</b> The recorder fetches by
/// guide and creates when absent; two first views racing both try the insert and the index
/// lets exactly one win, which is the entire concurrency story this row needs — it is
/// measurement, not state.
/// </para>
/// </summary>
public sealed class GuideAnalyticsConfiguration : IEntityTypeConfiguration<GuideAnalytics>
{
    public void Configure(EntityTypeBuilder<GuideAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideAnalyticsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(GuideValueConverters.Id<GuideAnalyticsId>());

        builder.Property(analytics => analytics.GuideId)
            .HasConversion(GuideValueConverters.Id<GuideId>())
            .IsRequired();

        builder.HasIndex(analytics => analytics.GuideId)
            .IsUnique()
            .HasDatabaseName("ux_guide_analytics_guide");

        builder.Ignore(analytics => analytics.DomainEvents);
    }
}

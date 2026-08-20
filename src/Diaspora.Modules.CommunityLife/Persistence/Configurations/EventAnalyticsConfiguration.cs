using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the per-event measured record.
/// <para>
/// Deliberately not soft-deletable. The figures outlive the event's visibility: an archived
/// festival's attendance still belongs in its organizer's history and in the platform's category
/// statistics, and putting them on a soft-deletable aggregate would hide them behind the global
/// query filter the moment somebody tidied up.
/// </para>
/// </summary>
public sealed class EventAnalyticsConfiguration : IEntityTypeConfiguration<EventAnalytics>
{
    public void Configure(EntityTypeBuilder<EventAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.EventAnalyticsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(CommunityLifeValueConverters.Id<EventAnalyticsId>());

        builder.Property(analytics => analytics.EventId)
            .HasConversion(CommunityLifeValueConverters.Id<CommunityEventId>())
            .IsRequired();

        builder.Property(analytics => analytics.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>());

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(CommunityLifePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(analytics => analytics.CategoryKey)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        // One record per event, and the index that resolves it on every settlement.
        builder.HasIndex(analytics => analytics.EventId)
            .IsUnique()
            .HasDatabaseName("ix_community_life_event_analytics_event");

        // The category rollup, scoped by country and windowed by completion.
        builder.HasIndex(analytics =>
                new { analytics.CountryCode, analytics.CategoryKey, analytics.CompletedAt })
            .HasDatabaseName("ix_community_life_event_analytics_category");

        // The popular-neighbourhoods figure.
        builder.HasIndex(analytics => analytics.NeighborhoodAreaId)
            .HasDatabaseName("ix_community_life_event_analytics_neighborhood");

        builder.HasIndex(analytics => analytics.OrganizationId)
            .HasDatabaseName("ix_community_life_event_analytics_organization");

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.ConversionRate);
        builder.Ignore(analytics => analytics.NoShowRate);
        builder.Ignore(analytics => analytics.AttendanceRate);
    }
}

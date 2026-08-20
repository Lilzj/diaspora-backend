using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>Maps the per-organization measured record. Outlives the page, like the event's.</summary>
public sealed class OrganizationAnalyticsConfiguration
    : IEntityTypeConfiguration<OrganizationAnalytics>
{
    public void Configure(EntityTypeBuilder<OrganizationAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.OrganizationAnalyticsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationAnalyticsId>());

        builder.Property(analytics => analytics.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>())
            .IsRequired();

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(CommunityLifePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(analytics => analytics.OrganizationTypeKey)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.HasIndex(analytics => analytics.OrganizationId)
            .IsUnique()
            .HasDatabaseName("ix_community_life_organization_analytics_organization");

        builder.HasIndex(analytics =>
                new { analytics.CountryCode, analytics.OrganizationTypeKey })
            .HasDatabaseName("ix_community_life_organization_analytics_type");

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.RetentionRate);
        builder.Ignore(analytics => analytics.AttendanceRate);
    }
}

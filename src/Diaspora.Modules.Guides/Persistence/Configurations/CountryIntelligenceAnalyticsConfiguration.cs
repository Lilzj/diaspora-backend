using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps per-country intelligence usage counters.
/// <para>
/// <b>No concurrency token, and that is a deliberate trade rather than an omission.</b> A lost
/// increment is one view among thousands; a conflict on the most-read screen during an emergency
/// would be an outage. B8.1 makes the same call for its three counter aggregates and the reasoning is
/// unchanged.
/// </para>
/// <para>
/// The counter collection keys on (country, kind). The kind is an open string, so a new signal is a
/// new row rather than a migration — the same open-set discipline B8.1's cross-vertical counters use.
/// </para>
/// </summary>
public sealed class CountryIntelligenceAnalyticsConfiguration
    : IEntityTypeConfiguration<CountryIntelligenceAnalytics>
{
    public void Configure(EntityTypeBuilder<CountryIntelligenceAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.CountryAnalyticsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(GuideValueConverters.Id<CountryIntelligenceAnalyticsId>());

        builder.Property(analytics => analytics.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.HasIndex(analytics => analytics.CountryCode)
            .IsUnique()
            .HasDatabaseName("ux_intel_country_analytics_country");

        builder.OwnsMany(analytics => analytics.Counters, counter =>
        {
            counter.ToTable(
                IntelligencePersistenceConstants.CountryCountersTable,
                GuidesModuleMetadata.Schema);

            counter.Property(value => value.Kind)
                .HasMaxLength(IntelligencePersistenceConstants.CounterKindLength)
                .IsRequired();

            counter.WithOwner().HasForeignKey("CountryIntelligenceAnalyticsId");

            counter.HasKey("CountryIntelligenceAnalyticsId", nameof(IntelligenceCounter.Kind));
        });

        builder.Ignore(analytics => analytics.DomainEvents);
    }
}

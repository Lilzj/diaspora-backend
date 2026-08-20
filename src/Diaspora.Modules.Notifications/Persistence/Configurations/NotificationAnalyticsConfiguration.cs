using Diaspora.Modules.Notifications.Domain.Analytics;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the daily rollup.
/// <para>
/// <b>The unique index on the grain is what makes the upsert safe.</b> Buckets are opened
/// concurrently by every request on the platform; without the constraint two rows for one
/// (day, category, channel, template) would appear under load and quietly halve every rate anybody
/// read. With it, the loser of the race fails and retries into the existing row.
/// </para>
/// <para>
/// The day is stored as a <see cref="DateOnly"/>, which every provider this platform targets maps
/// natively — and which makes a range query over a month a range scan rather than a computed
/// predicate.
/// </para>
/// </summary>
public sealed class NotificationAnalyticsConfiguration
    : IEntityTypeConfiguration<NotificationAnalytics>
{
    public void Configure(EntityTypeBuilder<NotificationAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.AnalyticsTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Revision).IsConcurrencyToken();

        builder.Property(analytics => analytics.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationAnalyticsId>());

        builder.Property(analytics => analytics.CategoryKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(analytics => analytics.TemplateKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(analytics => analytics.Channel)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(analytics => new
            {
                analytics.Day,
                analytics.CategoryKey,
                analytics.Channel,
                analytics.TemplateKey
            })
            .IsUnique()
            .HasDatabaseName("ix_notification_analytics_grain");

        builder.HasIndex(analytics => analytics.Day)
            .HasDatabaseName("ix_notification_analytics_day");

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.DeliveryRate);
        builder.Ignore(analytics => analytics.OpenRate);
        builder.Ignore(analytics => analytics.ClickThroughRate);
        builder.Ignore(analytics => analytics.DismissRate);
        builder.Ignore(analytics => analytics.FailureRate);
        builder.Ignore(analytics => analytics.AverageMinutesToOpen);
    }
}

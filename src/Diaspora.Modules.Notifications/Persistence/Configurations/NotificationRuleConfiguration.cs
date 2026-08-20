using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the routing catalogue.
/// <para>
/// <b>The index on the event name is the one the dispatcher hits on every consumed integration
/// event</b> — which is to say, on every meaningful thing that happens anywhere on the platform. It
/// is small, hot and cacheable, and it is the reason routing can be reference data rather than code
/// without costing anything measurable.
/// </para>
/// <para>
/// The channel set is a bitmask column rather than a child table. A join to read three flags on the
/// hottest path in the module would be a cost paid per notification for no expressiveness.
/// </para>
/// </summary>
public sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.RulesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Revision).IsConcurrencyToken();

        builder.Property(rule => rule.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationRuleId>());

        builder.Property(rule => rule.Key)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.EventName)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.Audience)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.TemplateKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.CategoryKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.OwnerModule)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.Priority)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(rule => rule.Policy)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(rule => rule.Key)
            .IsUnique()
            .HasDatabaseName("ix_notification_rules_key");

        builder.HasIndex(rule => new { rule.EventName, rule.IsActive })
            .HasDatabaseName("ix_notification_rules_event");

        builder.Ignore(rule => rule.DomainEvents);
        builder.Ignore(rule => rule.Channels);
    }
}

using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the category catalogue.
/// <para>
/// Retired with a flag rather than deleted: every notification and every preference stores the key,
/// and deleting a row would orphan the lot. The unique index is on the key alone, because unlike
/// templates there is exactly one version of a category.
/// </para>
/// </summary>
public sealed class NotificationCategoryConfiguration
    : IEntityTypeConfiguration<NotificationCategory>
{
    public void Configure(EntityTypeBuilder<NotificationCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.CategoriesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Revision).IsConcurrencyToken();

        builder.Property(category => category.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(category => category.DisplayName)
            .HasMaxLength(NotificationPersistenceConstants.TitleLength)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(NotificationPersistenceConstants.BodyLength);

        builder.Property(category => category.OwnerModule)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(category => category.DefaultPriority)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ix_notification_categories_key");

        builder.HasIndex(category => new { category.IsActive, category.Position })
            .HasDatabaseName("ix_notification_categories_order");

        builder.Ignore(category => category.DomainEvents);
    }
}

using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the notification aggregate and the three tables it owns.
/// <para>
/// <b>Every owned collection declares its own key and marks it never-generated.</b> Without
/// <c>HasKey</c>, EF invents a store-generated shadow integer inside a composite primary key, which
/// SQLite refuses on the second insert; with <c>HasKey</c> but without
/// <c>ValueGeneratedNever</c>, EF treats a populated key as an existing row and emits an UPDATE
/// that matches nothing, surfacing as a bogus concurrency conflict. Both of those cost a phase each
/// to find, which is why all four owned collections here spell it out.
/// </para>
/// <para>
/// <b>The indexes are the inbox screen.</b> The first is every list the member sees, and it carries
/// <c>CreatedAt</c> because the page is ordered by it and a sort that is not indexed is a sort of
/// the whole table. The unread index is partial in intent — the badge query — and the group index
/// serves de-duplication, which runs once per notification created and must not be a scan.
/// </para>
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.NotificationsTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Revision).IsConcurrencyToken();

        builder.Property(notification => notification.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationId>());

        builder.Property(notification => notification.RecipientUserId)
            .HasConversion(NotificationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(notification => notification.CategoryKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(notification => notification.TemplateKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(notification => notification.RuleKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength);

        builder.Property(notification => notification.GroupKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength);

        builder.Property(notification => notification.Priority)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(notification => notification.Status)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(notification => notification.StatusBeforeArchive)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength);

        builder.Property(notification => notification.ArchivedFor)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength);

        ConfigureContent(builder);
        ConfigureSource(builder);
        ConfigureLink(builder);
        ConfigureEngagement(builder);
        ConfigureDeliveries(builder);
        ConfigureAttempts(builder);
        ConfigureActions(builder);
        ConfigureIndexes(builder);

        builder.Ignore(notification => notification.DomainEvents);
        builder.Ignore(notification => notification.IsUnread);
        builder.Ignore(notification => notification.IsArchived);
        builder.Ignore(notification => notification.IsExpired);
        builder.Ignore(notification => notification.IsLive);
    }

    /// <summary>
    /// The rendered words, as columns on the notification row.
    /// <para>
    /// Owned-as-one rather than a table, because content is read on every single inbox row and a
    /// join for it would double the cost of the busiest query in the module.
    /// </para>
    /// </summary>
    private static void ConfigureContent(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsOne(notification => notification.Content, content =>
        {
            content.Property(value => value.Title)
                .HasColumnName("title")
                .HasMaxLength(NotificationPersistenceConstants.TitleLength)
                .IsRequired();

            content.Property(value => value.Body)
                .HasColumnName("body")
                .HasMaxLength(NotificationPersistenceConstants.BodyLength)
                .IsRequired();

            content.Property(value => value.ShortBody)
                .HasColumnName("short_body")
                .HasMaxLength(NotificationPersistenceConstants.ShortBodyLength);

            content.Property(value => value.Language)
                .HasColumnName("language")
                .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
                .IsRequired();

            content.Property(value => value.Format)
                .HasColumnName("content_format")
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.ImageMediaId).HasColumnName("image_media_id");

            content.Ignore(value => value.PushBody);
        });

    private static void ConfigureSource(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsOne(notification => notification.Source, source =>
        {
            source.Property(value => value.Module)
                .HasColumnName("source_module")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            source.Property(value => value.EventName)
                .HasColumnName("source_event_name")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            source.Property(value => value.SubjectType)
                .HasColumnName("subject_type")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            source.Property(value => value.SubjectId)
                .HasColumnName("subject_id")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            source.Ignore(value => value.DeduplicationKey);
        });

    private static void ConfigureLink(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsOne(notification => notification.PrimaryLink, link =>
        {
            link.Property(value => value.Route)
                .HasColumnName("route")
                .HasMaxLength(NotificationPersistenceConstants.RouteLength);

            link.Property(value => value.EntityType)
                .HasColumnName("link_entity_type")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            link.Property(value => value.EntityId)
                .HasColumnName("link_entity_id")
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);
        });

    private static void ConfigureEngagement(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsOne(notification => notification.Engagement, engagement =>
        {
            engagement.Property(value => value.OpenedAt).HasColumnName("opened_at");
            engagement.Property(value => value.ClickedAt).HasColumnName("clicked_at");
            engagement.Property(value => value.DismissedAt).HasColumnName("dismissed_at");
            engagement.Property(value => value.ArchivedAt).HasColumnName("archived_at");

            engagement.Property(value => value.OpenedOnChannel)
                .HasColumnName("opened_on_channel")
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);

            engagement.Ignore(value => value.IsOpened);
            engagement.Ignore(value => value.IsClicked);
            engagement.Ignore(value => value.IsDismissed);
            engagement.Ignore(value => value.IsArchived);
        });

    /// <summary>
    /// One row per channel.
    /// <para>
    /// The unique index on (notification, channel) is what makes <c>Queue</c> idempotent at the
    /// database rather than only in the aggregate — at-least-once delivery means the same fact
    /// arrives twice, and two rows for one channel would mean two pushes.
    /// </para>
    /// <para>
    /// The due index is the dispatcher's only query and it is the one that has to stay fast as the
    /// table grows into millions: status and next-attempt together, so a sweep reads a range rather
    /// than filtering a scan.
    /// </para>
    /// </summary>
    private static void ConfigureDeliveries(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsMany(notification => notification.Deliveries, delivery =>
        {
            delivery.ToTable(
                NotificationPersistenceConstants.DeliveriesTable,
                NotificationsModuleMetadata.Schema);

            delivery.HasKey(value => value.DeliveryId);
            delivery.Property(value => value.DeliveryId).ValueGeneratedNever();

            delivery.Property(value => value.Channel)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            delivery.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            delivery.Property(value => value.ReasonCode)
                .HasMaxLength(NotificationPersistenceConstants.ReasonLength);

            delivery.Property(value => value.ProviderMessageId)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            // Indexed by shadow property name: the owner key EF creates on an owned collection is
            // not a CLR property, so the expression overload cannot name it.
            delivery.HasIndex("NotificationId", nameof(NotificationDelivery.Channel))
                .IsUnique()
                .HasDatabaseName("ix_notification_deliveries_unique");

            delivery.HasIndex(value => new { value.Status, value.NextAttemptAt })
                .HasDatabaseName("ix_notification_deliveries_due");

            delivery.Ignore(value => value.IsTerminal);
            delivery.Ignore(value => value.IsRetryable);
        });

    /// <summary>
    /// The attempt log.
    /// <para>
    /// Flat on the notification with the channel as a column, rather than nested inside the delivery
    /// row. An owned collection inside an owned collection is legal and is a reliable source of
    /// composite-key surprises; this shape answers every question the nested one would.
    /// </para>
    /// </summary>
    private static void ConfigureAttempts(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsMany(notification => notification.Attempts, attempt =>
        {
            attempt.ToTable(
                NotificationPersistenceConstants.AttemptsTable,
                NotificationsModuleMetadata.Schema);

            attempt.HasKey(value => value.AttemptId);
            attempt.Property(value => value.AttemptId).ValueGeneratedNever();

            attempt.Property(value => value.Channel)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            attempt.Property(value => value.ReasonCode)
                .HasMaxLength(NotificationPersistenceConstants.ReasonLength);

            attempt.Property(value => value.Detail)
                .HasMaxLength(NotificationPersistenceConstants.ReasonLength);

            attempt.Property(value => value.ProviderMessageId)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength);

            attempt.HasIndex("NotificationId", nameof(NotificationDeliveryAttempt.AttemptedAt))
                .HasDatabaseName("ix_notification_attempts_timeline");
        });

    private static void ConfigureActions(EntityTypeBuilder<Notification> builder) =>
        builder.OwnsMany(notification => notification.Actions, action =>
        {
            action.ToTable(
                NotificationPersistenceConstants.ActionsTable,
                NotificationsModuleMetadata.Schema);

            action.HasKey(value => value.ActionId);
            action.Property(value => value.ActionId).ValueGeneratedNever();

            action.Property(value => value.Key)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            action.Property(value => value.Label)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            action.Property(value => value.Style)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            action.OwnsOne(value => value.Link, link =>
            {
                link.Property(value => value.Route)
                    .HasColumnName("route")
                    .HasMaxLength(NotificationPersistenceConstants.RouteLength);

                link.Property(value => value.EntityType)
                    .HasColumnName("link_entity_type")
                    .HasMaxLength(NotificationPersistenceConstants.KeyLength);

                link.Property(value => value.EntityId)
                    .HasColumnName("link_entity_id")
                    .HasMaxLength(NotificationPersistenceConstants.KeyLength);
            });
        });

    /// <summary>
    /// The four reads that must not degrade as the table reaches millions of rows.
    /// <para>
    /// <b>The inbox index is the module's most important object.</b> Every list screen is
    /// "this member, not archived, newest first", and the cursor pages on <c>CreatedAt</c> — so the
    /// recipient, the status and the timestamp have to be one index or every page is a sort of the
    /// member's whole history.
    /// </para>
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<Notification> builder)
    {
        builder.HasIndex(notification => new
            {
                notification.RecipientUserId,
                notification.Status,
                notification.CreatedAt
            })
            .HasDatabaseName("ix_notifications_inbox");

        // The badge. Kept separate from the inbox index because it filters on the category too, and
        // a per-category unread count is what the settings and filter screens both read.
        builder.HasIndex(notification => new
            {
                notification.RecipientUserId,
                notification.CategoryKey,
                notification.Status
            })
            .HasDatabaseName("ix_notifications_category");

        // De-duplication, which runs once per notification created and must never be a scan.
        builder.HasIndex(notification => new
            {
                notification.RecipientUserId,
                notification.TemplateKey,
                notification.GroupKey,
                notification.CreatedAt
            })
            .HasDatabaseName("ix_notifications_deduplication");

        // The retention sweep.
        builder.HasIndex(notification => notification.ExpiresAt)
            .HasDatabaseName("ix_notifications_expiry");
    }
}

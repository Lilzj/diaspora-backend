using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the digest aggregate and its entries.
/// <para>
/// The window index is what makes generation idempotent: the sweep runs hourly and a schedule fires
/// on the hour, so a restart inside that hour would otherwise build and send the same digest twice.
/// </para>
/// </summary>
public sealed class NotificationDigestConfiguration : IEntityTypeConfiguration<NotificationDigest>
{
    public void Configure(EntityTypeBuilder<NotificationDigest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.DigestsTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(digest => digest.Id);

        builder.Property(digest => digest.Revision).IsConcurrencyToken();

        builder.Property(digest => digest.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationDigestId>());

        builder.Property(digest => digest.RecipientUserId)
            .HasConversion(NotificationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(digest => digest.Frequency)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(digest => digest.Status)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(digest => digest.CategoryKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength);

        builder.Property(digest => digest.CountryCode).HasMaxLength(8);

        builder.Property(digest => digest.Language)
            .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
            .IsRequired();

        builder.OwnsMany(digest => digest.Entries, entry =>
        {
            entry.ToTable(
                NotificationPersistenceConstants.DigestEntriesTable,
                NotificationsModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.NotificationId)
                .HasConversion(NotificationValueConverters.Id<NotificationId>())
                .IsRequired();

            entry.Property(value => value.CategoryKey)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            entry.Property(value => value.Title)
                .HasMaxLength(NotificationPersistenceConstants.TitleLength)
                .IsRequired();

            entry.Property(value => value.Summary)
                .HasMaxLength(NotificationPersistenceConstants.ShortBodyLength)
                .IsRequired();

            entry.Property(value => value.Route)
                .HasMaxLength(NotificationPersistenceConstants.RouteLength);
        });

        builder.HasIndex(digest => new
            {
                digest.RecipientUserId,
                digest.Frequency,
                digest.WindowStart,
                digest.WindowEnd
            })
            .HasDatabaseName("ix_notification_digests_window");

        builder.HasIndex(digest => new { digest.RecipientUserId, digest.GeneratedAt })
            .HasDatabaseName("ix_notification_digests_recipient");

        builder.Ignore(digest => digest.DomainEvents);
        builder.Ignore(digest => digest.IsEmpty);
    }
}

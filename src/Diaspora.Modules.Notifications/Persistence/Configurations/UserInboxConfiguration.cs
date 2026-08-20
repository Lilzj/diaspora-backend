using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the counter aggregate.
/// <para>
/// <b>One row per member, and the unique index on the member is what makes that true.</b> The
/// counters are read on every app launch by every member; two rows for one person would mean a badge
/// that changed depending on which one the query happened to return, and the race that creates them
/// — two notifications arriving at once for a member with no inbox yet — is the normal case rather
/// than the exception.
/// </para>
/// </summary>
public sealed class UserInboxConfiguration : IEntityTypeConfiguration<UserInbox>
{
    public void Configure(EntityTypeBuilder<UserInbox> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.InboxesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(inbox => inbox.Id);

        builder.Property(inbox => inbox.Revision).IsConcurrencyToken();

        builder.Property(inbox => inbox.Id)
            .HasConversion(NotificationValueConverters.Id<UserInboxId>());

        builder.Property(inbox => inbox.UserId)
            .HasConversion(NotificationValueConverters.Id<UserId>())
            .IsRequired();

        builder.OwnsMany(inbox => inbox.Categories, counter =>
        {
            counter.ToTable(
                NotificationPersistenceConstants.InboxCountersTable,
                NotificationsModuleMetadata.Schema);

            counter.HasKey(value => value.CounterId);
            counter.Property(value => value.CounterId).ValueGeneratedNever();

            counter.Property(value => value.CategoryKey)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            counter.HasIndex("UserInboxId", nameof(Domain.ValueObjects.InboxCategoryCounter.CategoryKey))
                .IsUnique()
                .HasDatabaseName("ix_user_inbox_counters_unique");
        });

        builder.HasIndex(inbox => inbox.UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_inboxes_user");

        // The reconciliation sweep picks the least recently checked. Without this it would sort the
        // whole table to find them.
        builder.HasIndex(inbox => inbox.ModifiedAt)
            .HasDatabaseName("ix_user_inboxes_stale");

        builder.Ignore(inbox => inbox.DomainEvents);
    }
}

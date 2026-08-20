using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the preference aggregate, its two owned collections and its two owned value objects.
/// <para>
/// <b>Quiet hours and the digest schedule are columns, not tables.</b> Both are read on every single
/// dispatch — the policy resolver consults them for every channel of every message — and a join for
/// five integers would put two extra reads on the platform's hottest write path.
/// </para>
/// <para>
/// <b>The category preferences are a table, and that is the opposite decision for the opposite
/// reason.</b> There are sixteen categories today and a deployment may add more; sixteen nullable
/// column triples on one row would be a migration every time somebody invents a subject area.
/// </para>
/// </summary>
public sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.PreferencesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(preference => preference.Id);

        builder.Property(preference => preference.Revision).IsConcurrencyToken();

        builder.Property(preference => preference.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationPreferenceId>());

        builder.Property(preference => preference.UserId)
            .HasConversion(NotificationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(preference => preference.Language)
            .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
            .IsRequired();

        builder.Property(preference => preference.TimeZoneId)
            .HasMaxLength(NotificationPersistenceConstants.TimeZoneLength)
            .IsRequired();

        builder.OwnsOne(preference => preference.QuietHours, quiet =>
        {
            quiet.Property(value => value.IsEnabled).HasColumnName("quiet_hours_enabled");
            quiet.Property(value => value.StartHour).HasColumnName("quiet_hours_start");
            quiet.Property(value => value.EndHour).HasColumnName("quiet_hours_end");
        });

        builder.OwnsOne(preference => preference.Digest, digest =>
        {
            digest.Property(value => value.Frequency)
                .HasColumnName("digest_frequency")
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);

            digest.Property(value => value.HourOfDay).HasColumnName("digest_hour");

            digest.Property(value => value.DayOfWeek)
                .HasColumnName("digest_day_of_week")
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);

            digest.Ignore(value => value.IsActive);
            digest.Ignore(value => value.Window);
        });

        builder.OwnsMany(preference => preference.Channels, channel =>
        {
            channel.ToTable(
                NotificationPersistenceConstants.ChannelPreferencesTable,
                NotificationsModuleMetadata.Schema);

            // A caller-supplied key would be better, but a channel preference is identified by its
            // channel within its owner and has no natural Guid. The composite key states that
            // directly, which also makes the uniqueness a constraint rather than an index.
            channel.HasKey("NotificationPreferenceId", nameof(ChannelPreference.Channel));

            channel.Property(value => value.Channel)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();
        });

        builder.OwnsMany(preference => preference.Categories, category =>
        {
            category.ToTable(
                NotificationPersistenceConstants.CategoryPreferencesTable,
                NotificationsModuleMetadata.Schema);

            category.HasKey("NotificationPreferenceId", nameof(CategoryPreference.CategoryKey));

            category.Property(value => value.CategoryKey)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            // B13.3. Nullable and unindexed. Nothing queries by them — they are read alongside the
            // row that was already loaded by member — and a source is one of three short literals,
            // so the reason column is sized as a key rather than as prose.
            category.Property(value => value.UnsubscribeSource)
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);
        });

        builder.HasIndex(preference => preference.UserId)
            .IsUnique()
            .HasDatabaseName("ix_notification_preferences_user");

        builder.Ignore(preference => preference.DomainEvents);
        builder.Ignore(preference => preference.IsFullyOptedOut);
    }
}

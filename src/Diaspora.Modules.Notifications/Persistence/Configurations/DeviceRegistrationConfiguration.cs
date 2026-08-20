using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the delivery-endpoint aggregate.
/// <para>
/// <b>The unique index is on (user, channel, token) and that is the shape that stops duplicate
/// pushes.</b> A token can migrate between devices — a backup restored onto a new phone — so keying
/// on the device string would let the same token be registered twice and every message be delivered
/// twice. Keying on the token itself makes re-registration a refresh, which is what it is.
/// </para>
/// <para>
/// The token column is indexed as part of that constraint and is never selected into a response,
/// logged, or published. It is a credential for somebody else's service.
/// </para>
/// </summary>
public sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.DevicesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(device => device.Id);

        builder.Property(device => device.Revision).IsConcurrencyToken();

        builder.Property(device => device.Id)
            .HasConversion(NotificationValueConverters.Id<DeviceRegistrationId>());

        builder.Property(device => device.UserId)
            .HasConversion(NotificationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(device => device.Channel)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(device => device.Platform)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.PlatformLength)
            .IsRequired();

        builder.Property(device => device.Token)
            .HasMaxLength(NotificationPersistenceConstants.TokenLength)
            .IsRequired();

        builder.Property(device => device.DeviceIdentifier)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength);

        builder.Property(device => device.FriendlyName)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength);

        builder.Property(device => device.AppVersion)
            .HasMaxLength(NotificationPersistenceConstants.EnumLength);

        builder.Property(device => device.SuppressionReason)
            .HasMaxLength(NotificationPersistenceConstants.ReasonLength);

        builder.HasIndex(device => new { device.UserId, device.Channel, device.Token })
            .IsUnique()
            .HasDatabaseName("ix_notification_devices_token");

        // The dispatcher's only device query: everything a member can be reached on.
        builder.HasIndex(device => new { device.UserId, device.Channel, device.IsActive })
            .HasDatabaseName("ix_notification_devices_deliverable");

        builder.HasIndex(device => new { device.UserId, device.DeviceIdentifier })
            .HasDatabaseName("ix_notification_devices_identifier");

        // B13.3. Not indexed, and deliberately: the only query that reads it is "which of this
        // member's endpoints is the current one", which is answered in memory over a list already
        // loaded by member. An index here would be a second B-tree maintained on every registration
        // to serve no query the platform makes.
        //
        // Not unique either. One device legitimately holds several endpoints — a rotated token before
        // the old row is retired, and a channel other than push when one arrives — and a uniqueness
        // constraint would turn an ordinary token rotation into a 409 on a path a client retries.
        builder.Ignore(device => device.DomainEvents);
        builder.Ignore(device => device.IsDeliverable);
    }
}

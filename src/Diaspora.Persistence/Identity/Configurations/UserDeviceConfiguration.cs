using Diaspora.Domain.Identity.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps the <see cref="UserDevice"/> aggregate.
/// </summary>
public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.Devices, IdentitySchema.Name);

        builder.HasKey(device => device.Id);

        builder.Property(device => device.UserId).IsRequired();

        builder.Property(device => device.DeviceIdentifier)
            .HasMaxLength(UserDevice.MaxIdentifierLength)
            .IsRequired();

        builder.Property(device => device.FriendlyName)
            .HasMaxLength(UserDevice.MaxFriendlyNameLength)
            .IsRequired();

        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(device => device.AppVersion).HasMaxLength(40);
        builder.Property(device => device.PushToken).HasMaxLength(UserDevice.MaxPushTokenLength);
        builder.Property(device => device.LastActiveAt).IsRequired();
        builder.Property(device => device.IsTrusted).IsRequired();

        // Scoped to the user, not global: two members may legitimately report the same
        // identifier (a shared or reset device), and a global unique index would lock the
        // second one out.
        builder.HasIndex(device => new { device.UserId, device.DeviceIdentifier })
            .IsUnique()
            .HasDatabaseName("ix_user_devices_user_identifier");

        builder.Ignore(device => device.DomainEvents);
    }
}

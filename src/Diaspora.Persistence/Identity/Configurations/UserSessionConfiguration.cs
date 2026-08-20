using Diaspora.Domain.Identity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps the <see cref="UserSession"/> aggregate and its refresh-token rotation chain.
/// </summary>
public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.Sessions, IdentitySchema.Name);

        builder.HasKey(session => session.Id);

        builder.Property(session => session.UserId).IsRequired();
        builder.Property(session => session.DeviceName).HasMaxLength(UserSession.MaxDeviceNameLength).IsRequired();
        builder.Property(session => session.Platform).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.AppVersion).HasMaxLength(40);
        builder.Property(session => session.IpAddress).HasMaxLength(64);
        builder.Property(session => session.UserAgent).HasMaxLength(400);
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.RevocationReason).HasConversion<string>().HasMaxLength(40);
        builder.Property(session => session.LastSeenAt).IsRequired();
        builder.Property(session => session.AbsoluteExpiresAt).IsRequired();

        // Mapped as a related entity, not an owned collection — see RefreshTokenConfiguration
        // for why. Cascade delete keeps the chain tied to the session's lifetime.
        builder.HasMany(session => session.RefreshTokens)
            .WithOne()
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(UserSession.RefreshTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(session => new { session.UserId, session.Status })
            .HasDatabaseName("ix_user_sessions_user_status");

        builder.HasIndex(session => session.DeviceId)
            .HasDatabaseName("ix_user_sessions_device_id");

        builder.Ignore(session => session.DomainEvents);
    }
}

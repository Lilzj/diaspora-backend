using Diaspora.Domain.Identity.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps the append-only <see cref="SecurityEvent"/> log.
/// </summary>
public sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.SecurityEvents, IdentitySchema.Name);

        builder.HasKey(securityEvent => securityEvent.Id);

        builder.Property(securityEvent => securityEvent.Type).HasConversion<string>().HasMaxLength(60).IsRequired();
        builder.Property(securityEvent => securityEvent.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(securityEvent => securityEvent.Description).HasMaxLength(SecurityEvent.MaxDescriptionLength);
        builder.Property(securityEvent => securityEvent.IpAddress).HasMaxLength(64);
        builder.Property(securityEvent => securityEvent.UserAgent).HasMaxLength(400);
        builder.Property(securityEvent => securityEvent.DeviceIdentifier).HasMaxLength(200);
        builder.Property(securityEvent => securityEvent.CorrelationId).HasMaxLength(64);
        builder.Property(securityEvent => securityEvent.OccurredAt).IsRequired();

        // Left as text rather than a Postgres jsonb column so the same mapping works on
        // SQLite in the integration suite. Nothing queries inside it today.
        builder.Property(securityEvent => securityEvent.MetadataJson);

        builder.HasIndex(securityEvent => new { securityEvent.UserId, securityEvent.OccurredAt })
            .HasDatabaseName("ix_security_events_user_occurred");

        builder.HasIndex(securityEvent => new { securityEvent.Type, securityEvent.OccurredAt })
            .HasDatabaseName("ix_security_events_type_occurred");

        builder.Ignore(securityEvent => securityEvent.DomainEvents);
    }
}

using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the residency projection.
/// <para>
/// A projection table, and named like one. It holds what Trust decided, never what Location
/// decided, and every row is written by an integration-event handler rather than by a command.
/// If the table were dropped it could be rebuilt entirely by replaying Trust's events, which
/// is the property that makes it a cache rather than a second source of truth.
/// </para>
/// </summary>
public sealed class VerifiedResidencyConfiguration : IEntityTypeConfiguration<VerifiedResidency>
{
    public void Configure(EntityTypeBuilder<VerifiedResidency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            LocationPersistenceConstants.VerifiedResidenciesTable, LocationModuleMetadata.Schema);

        builder.HasKey(residency => residency.Id);

        builder.Property(residency => residency.Id)
            .HasConversion(LocationValueConverters.Id<VerifiedResidencyId>());

        builder.Property(residency => residency.UserId)
            .HasConversion(LocationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(residency => residency.CountryCode)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(residency => residency.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(residency => residency.VerificationId);

        builder.Property(residency => residency.DecidedAt).IsRequired();
        builder.Property(residency => residency.ExpiresAt);
        builder.Property(residency => residency.ObservedAt).IsRequired();

        builder.Property(residency => residency.Reason)
            .HasMaxLength(LocationPersistenceConstants.ReasonLength);

        builder.HasIndex(residency => new { residency.UserId, residency.CountryCode })
            .IsUnique()
            .HasDatabaseName("ix_verified_residencies_user_country");

        // "Which grants lapse this week" — the sweep that turns an expiry into a state change.
        builder.HasIndex(residency => new { residency.State, residency.ExpiresAt })
            .HasDatabaseName("ix_verified_residencies_state_expiry");

        builder.Ignore(residency => residency.DomainEvents);
    }
}

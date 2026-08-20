using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the register of issuing authorities.
/// <para>
/// <b>The index on the verification case is the one that makes Trust integration work.</b> A decision
/// arrives from Trust carrying its own case identifier and nothing this module chose, so the inbound
/// projection's only way to find the authority is a lookup on that column — and without an index it
/// is a scan of the register on every Trust event the platform emits, for every module, whether or
/// not it concerns an authority.
/// </para>
/// <para>
/// <b>No unique index on (country, name), deliberately.</b> The obvious constraint is wrong twice
/// over: a country genuinely has two bodies whose recorded names collide after transliteration, and a
/// restructured ministry legitimately appears twice while the old one is being retired. Uniqueness of
/// an institution is an editorial judgement, not a string comparison.
/// </para>
/// </summary>
public sealed class IssuingAuthorityConfiguration : IEntityTypeConfiguration<IssuingAuthority>
{
    public void Configure(EntityTypeBuilder<IssuingAuthority> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.AuthoritiesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(authority => authority.Id);

        builder.Property(authority => authority.Id)
            .HasConversion(GuideValueConverters.Id<IssuingAuthorityId>());

        builder.Property(authority => authority.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(authority => authority.Name)
            .HasMaxLength(IntelligenceLimits.MaxAuthorityName)
            .IsRequired();

        builder.Property(authority => authority.ShortName)
            .HasMaxLength(IntelligenceLimits.MaxAuthorityShortName);

        builder.Property(authority => authority.AuthorityType)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(authority => authority.Description)
            .HasMaxLength(IntelligenceLimits.MaxAuthorityDescription);

        builder.Property(authority => authority.Website)
            .HasMaxLength(IntelligenceLimits.MaxUrl);

        builder.Property(authority => authority.ContactEmail)
            .HasMaxLength(IntelligenceLimits.MaxEmail);

        builder.Property(authority => authority.ContactPhone)
            .HasMaxLength(IntelligenceLimits.MaxPhone);

        builder.Property(authority => authority.Revision).IsConcurrencyToken();

        // "Which bodies issue for this country, and which of them are verified" — the picker on
        // every notice editing screen.
        builder.HasIndex(authority => new { authority.CountryCode, authority.IsVerified })
            .HasDatabaseName("ix_intel_authorities_country_verified");

        // Where an inbound Trust decision lands.
        builder.HasIndex(authority => authority.VerificationCaseId)
            .HasDatabaseName("ix_intel_authorities_verification_case");

        builder.Ignore(authority => authority.DomainEvents);
        builder.Ignore(authority => authority.IsActive);
    }
}

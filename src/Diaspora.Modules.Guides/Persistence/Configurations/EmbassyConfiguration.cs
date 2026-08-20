using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the embassy directory and the three collections a mission owns.
/// <para>
/// <b>Two country indexes rather than one composite, and that is the shape of the two questions.</b>
/// "Which missions are in this country" and "which missions serve this country's nationals" are
/// asked separately, almost never together, and a composite over the pair would help neither. The
/// capability flags ride on both because every directory screen filters on them.
/// </para>
/// <para>
/// <b>No foreign key to the authority register, deliberately.</b> The link is optional and points at
/// a soft-deletable aggregate; a real constraint would mean archiving an authority either fails or
/// cascades into the directory, and neither is what an editor archiving a superseded ministry record
/// means. The identifier is stored and resolved, exactly as every cross-aggregate reference in this
/// module is.
/// </para>
/// </summary>
public sealed class EmbassyConfiguration : IEntityTypeConfiguration<Embassy>
{
    public void Configure(EntityTypeBuilder<Embassy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IntelligencePersistenceConstants.EmbassiesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(embassy => embassy.Id);

        builder.Property(embassy => embassy.Id)
            .HasConversion(GuideValueConverters.Id<EmbassyId>());

        ConfigurePlacement(builder);
        ConfigureContact(builder);
        ConfigureIndexes(builder);

        builder.Property(embassy => embassy.Revision).IsConcurrencyToken();

        ConfigureOfficeHours(builder);
        ConfigureServices(builder);
        ConfigureLocalizations(builder);

        builder.Ignore(embassy => embassy.DomainEvents);
        builder.Ignore(embassy => embassy.IsActive);
    }

    private static void ConfigurePlacement(EntityTypeBuilder<Embassy> builder)
    {
        builder.Property(embassy => embassy.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(embassy => embassy.ServesCountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(embassy => embassy.Name)
            .HasMaxLength(IntelligenceLimits.MaxEmbassyName)
            .IsRequired();

        builder.Property(embassy => embassy.AuthorityId)
            .HasConversion(GuideValueConverters.Id<IssuingAuthorityId>());

        builder.Property(embassy => embassy.City)
            .HasMaxLength(IntelligenceLimits.MaxRegion);

        builder.Property(embassy => embassy.Address)
            .HasMaxLength(IntelligenceLimits.MaxAddress);
    }

    private static void ConfigureContact(EntityTypeBuilder<Embassy> builder)
    {
        builder.Property(embassy => embassy.Telephone)
            .HasMaxLength(IntelligenceLimits.MaxPhone);

        builder.Property(embassy => embassy.EmergencyTelephone)
            .HasMaxLength(IntelligenceLimits.MaxPhone);

        builder.Property(embassy => embassy.Email)
            .HasMaxLength(IntelligenceLimits.MaxEmail);

        builder.Property(embassy => embassy.Website)
            .HasMaxLength(IntelligenceLimits.MaxUrl);

        builder.Property(embassy => embassy.AppointmentUrl)
            .HasMaxLength(IntelligenceLimits.MaxUrl);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Embassy> builder)
    {
        builder.HasIndex(embassy => new { embassy.CountryCode, embassy.IsVerified })
            .HasDatabaseName("ix_intel_embassies_location");

        builder.HasIndex(embassy => new { embassy.ServesCountryCode, embassy.IsVerified })
            .HasDatabaseName("ix_intel_embassies_serves");

        builder.HasIndex(embassy => embassy.VerificationCaseId)
            .HasDatabaseName("ix_intel_embassies_verification_case");
    }

    private static void ConfigureOfficeHours(EntityTypeBuilder<Embassy> builder) =>
        builder.OwnsMany(embassy => embassy.OfficeHours, hours =>
        {
            hours.ToTable(
                IntelligencePersistenceConstants.EmbassyOfficeHoursTable,
                GuidesModuleMetadata.Schema);

            hours.Property(value => value.Day)
                .HasConversion<string>()
                .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
                .IsRequired();

            hours.Property(value => value.Hours)
                .HasMaxLength(IntelligenceLimits.MaxOfficeHours);

            hours.WithOwner().HasForeignKey("EmbassyId");

            // The day is the identity: the aggregate deduplicates by day, so (mission, day) is
            // naturally unique and no surrogate is needed.
            hours.HasKey("EmbassyId", nameof(EmbassyOfficeHours.Day));
        });

    private static void ConfigureServices(EntityTypeBuilder<Embassy> builder) =>
        builder.OwnsMany(embassy => embassy.Services, service =>
        {
            service.ToTable(
                IntelligencePersistenceConstants.EmbassyServicesTable,
                GuidesModuleMetadata.Schema);

            service.Property(value => value.Key)
                .HasMaxLength(IntelligenceLimits.MaxCategoryKey)
                .IsRequired();

            service.Property(value => value.Name)
                .HasMaxLength(IntelligenceLimits.MaxServiceName)
                .IsRequired();

            service.Property(value => value.Description)
                .HasMaxLength(IntelligenceLimits.MaxServiceDescription);

            service.WithOwner().HasForeignKey("EmbassyId");

            service.HasKey("EmbassyId", nameof(EmbassyService.Key));
        });

    private static void ConfigureLocalizations(EntityTypeBuilder<Embassy> builder) =>
        builder.OwnsMany(embassy => embassy.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.EmbassyLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Name)
                .HasMaxLength(IntelligenceLimits.MaxEmbassyName)
                .IsRequired();

            localization.Property(value => value.Description)
                .HasMaxLength(IntelligenceLimits.MaxAuthorityDescription);

            localization.Property(value => value.Address)
                .HasMaxLength(IntelligenceLimits.MaxAddress);

            localization.WithOwner().HasForeignKey("EmbassyId");

            localization.HasKey("EmbassyId", nameof(EmbassyLocalization.Locale));
        });
}

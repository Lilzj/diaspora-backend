using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps one country's emergency information and the two collections it owns.
/// <para>
/// <b>The unique index on the country code is the aggregate's whole invariant.</b> One country, one
/// emergency directory: two rows would give two answers to "what is the police number here", both
/// visible, both plausible, and the one somebody sees decided by whichever the query ordered first.
/// The database refuses the second rather than trusting a handler to have checked.
/// </para>
/// <para>
/// <b>This is the only aggregate in the phase that is not soft-deletable</b>, so nothing here is
/// hidden by the global query filter and there is no restore path to keep working. A hidden row
/// holding a number that now rings somewhere else is a liability with no upside.
/// </para>
/// </summary>
public sealed class CountryEmergencyDirectoryConfiguration
    : IEntityTypeConfiguration<CountryEmergencyDirectory>
{
    public void Configure(EntityTypeBuilder<CountryEmergencyDirectory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.EmergencyDirectoriesTable,
            GuidesModuleMetadata.Schema);

        builder.HasKey(directory => directory.Id);

        builder.Property(directory => directory.Id)
            .HasConversion(GuideValueConverters.Id<CountryEmergencyDirectoryId>());

        builder.Property(directory => directory.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(directory => directory.Guidance)
            .HasMaxLength(IntelligenceLimits.MaxEmergencyNote);

        builder.Property(directory => directory.LastReviewedBy)
            .HasConversion(GuideValueConverters.Id<UserId>());

        builder.Property(directory => directory.Revision).IsConcurrencyToken();

        builder.HasIndex(directory => directory.CountryCode)
            .IsUnique()
            .HasDatabaseName("ux_intel_emergency_directories_country");

        ConfigureContacts(builder);
        ConfigureLocalizations(builder);

        builder.Ignore(directory => directory.DomainEvents);
    }

    private static void ConfigureContacts(
        EntityTypeBuilder<CountryEmergencyDirectory> builder) =>
        builder.OwnsMany(directory => directory.Contacts, contact =>
        {
            contact.ToTable(
                IntelligencePersistenceConstants.EmergencyContactsTable,
                GuidesModuleMetadata.Schema);

            contact.Property(value => value.Kind)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyKind)
                .IsRequired();

            contact.Property(value => value.Label)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyLabel)
                .IsRequired();

            contact.Property(value => value.Number)
                .HasMaxLength(IntelligenceLimits.MaxPhone)
                .IsRequired();

            contact.Property(value => value.AlternateNumber)
                .HasMaxLength(IntelligenceLimits.MaxPhone);

            contact.Property(value => value.Note)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyNote);

            contact.WithOwner().HasForeignKey("CountryEmergencyDirectoryId");

            // The kind is the identity, and the aggregate refuses a duplicate outright rather than
            // collapsing it: two police numbers is a contradiction an editor must resolve.
            contact.HasKey("CountryEmergencyDirectoryId", nameof(EmergencyContact.Kind));
        });

    private static void ConfigureLocalizations(
        EntityTypeBuilder<CountryEmergencyDirectory> builder) =>
        builder.OwnsMany(directory => directory.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.EmergencyContactLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Kind)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyKind)
                .IsRequired();

            localization.Property(value => value.Label)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyLabel)
                .IsRequired();

            localization.Property(value => value.Note)
                .HasMaxLength(IntelligenceLimits.MaxEmergencyNote);

            localization.WithOwner().HasForeignKey("CountryEmergencyDirectoryId");

            // Three parts, because a translation is of one contact in one language. Flattened onto
            // the directory rather than nested inside the contact — see the value object for why.
            localization.HasKey(
                "CountryEmergencyDirectoryId",
                nameof(EmergencyContactLocalization.Locale),
                nameof(EmergencyContactLocalization.Kind));
        });
}

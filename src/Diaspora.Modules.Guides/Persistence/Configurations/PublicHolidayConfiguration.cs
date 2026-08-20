using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the public holiday calendar and its translations.
/// <para>
/// <b><c>Year</c> is denormalised and indexed, and that is the one interesting decision here.</b>
/// "This country's holidays for 2026" is the query the calendar screen makes, and no provider can use
/// an index on a timestamp through a year function — so the choice is a stored integer or a table
/// scan on a screen that opens on every country page. The aggregate keeps it in step with the date on
/// every write, which is the only place the two could ever diverge.
/// </para>
/// <para>
/// <b>No unique constraint on (country, date, name).</b> Two observances genuinely fall on one day in
/// several countries, and the same observance legitimately appears twice while an editor is
/// correcting a region. Uniqueness of a holiday is an editorial judgement, not a string comparison.
/// </para>
/// </summary>
public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.PublicHolidaysTable, GuidesModuleMetadata.Schema);

        builder.HasKey(holiday => holiday.Id);

        builder.Property(holiday => holiday.Id)
            .HasConversion(GuideValueConverters.Id<PublicHolidayId>());

        builder.Property(holiday => holiday.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(holiday => holiday.Name)
            .HasMaxLength(IntelligenceLimits.MaxHolidayName)
            .IsRequired();

        builder.Property(holiday => holiday.Description)
            .HasMaxLength(IntelligenceLimits.MaxHolidayDescription);

        builder.Property(holiday => holiday.Kind)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(holiday => holiday.Region)
            .HasMaxLength(IntelligenceLimits.MaxRegion);

        builder.Property(holiday => holiday.Revision).IsConcurrencyToken();

        // The calendar screen: one country, one year, in date order.
        builder.HasIndex(holiday => new { holiday.CountryCode, holiday.Year })
            .HasDatabaseName("ix_intel_public_holidays_country_year");

        // The dashboard's ninety-day window, which is a range rather than a year.
        builder.HasIndex(holiday => new { holiday.CountryCode, holiday.ObservedOn })
            .HasDatabaseName("ix_intel_public_holidays_country_date");

        ConfigureLocalizations(builder);

        builder.Ignore(holiday => holiday.DomainEvents);
        builder.Ignore(holiday => holiday.IsActive);
    }

    private static void ConfigureLocalizations(EntityTypeBuilder<PublicHoliday> builder) =>
        builder.OwnsMany(holiday => holiday.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.PublicHolidayLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Name)
                .HasMaxLength(IntelligenceLimits.MaxHolidayName)
                .IsRequired();

            localization.Property(value => value.Description)
                .HasMaxLength(IntelligenceLimits.MaxHolidayDescription);

            localization.WithOwner().HasForeignKey("PublicHolidayId");

            localization.HasKey("PublicHolidayId", nameof(PublicHolidayLocalization.Locale));
        });
}

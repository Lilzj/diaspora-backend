using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps visa advisories and the three collections they own.
/// <para>
/// <b>The index is over the pair, and the nullable half is why it is worth having.</b> Every reader
/// query is "the rules for this destination that apply to me", which means matching a nationality
/// <em>or</em> null in one predicate — and a nullable column inside an index is exactly what makes
/// that a seek rather than a scan of a table that grows by nationality.
/// </para>
/// <para>
/// The lines collection keys on (advisory, kind, position). Position is part of the identity rather
/// than a payload column because the aggregate renumbers from zero within each kind on every write,
/// so it is stable by construction — and a surrogate would be a store-generated column inside a
/// composite key, which is the shape SQLite refuses to insert.
/// </para>
/// </summary>
public sealed class VisaAdvisoryConfiguration : IEntityTypeConfiguration<VisaAdvisory>
{
    public void Configure(EntityTypeBuilder<VisaAdvisory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.VisaAdvisoriesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(advisory => advisory.Id);

        builder.Property(advisory => advisory.Id)
            .HasConversion(GuideValueConverters.Id<VisaAdvisoryId>());

        ConfigureSubject(builder);
        ConfigureContent(builder);

        builder.Property(advisory => advisory.LastReviewedBy)
            .HasConversion(GuideValueConverters.Id<UserId>());

        builder.Property(advisory => advisory.Revision).IsConcurrencyToken();

        // "The rules for this destination that apply to me" — a nationality or nobody.
        builder.HasIndex(advisory => new
        {
            advisory.CountryCode,
            advisory.AppliesToCountryCode,
            advisory.Status
        })
            .HasDatabaseName("ix_intel_visa_advisories_country_applies");

        builder.HasIndex(advisory => advisory.AuthorityId)
            .HasDatabaseName("ix_intel_visa_advisories_authority");

        ConfigureLines(builder);
        ConfigureRelatedGuides(builder);
        ConfigureLocalizations(builder);

        builder.Ignore(advisory => advisory.DomainEvents);

        // Projections over the lines collection, computed on read. Mapping them would store the
        // same rows twice and give two answers the first time somebody edited one of them.
        builder.Ignore(advisory => advisory.Requirements);
        builder.Ignore(advisory => advisory.Documents);
    }

    private static void ConfigureSubject(EntityTypeBuilder<VisaAdvisory> builder)
    {
        builder.Property(advisory => advisory.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(advisory => advisory.AppliesToCountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength);

        builder.Property(advisory => advisory.AuthorityId)
            .HasConversion(GuideValueConverters.Id<IssuingAuthorityId>())
            .IsRequired();

        builder.Property(advisory => advisory.VisaCategory)
            .HasMaxLength(IntelligenceLimits.MaxVisaCategory);

        builder.Property(advisory => advisory.Status)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();
    }

    private static void ConfigureContent(EntityTypeBuilder<VisaAdvisory> builder)
    {
        builder.Property(advisory => advisory.Title)
            .HasMaxLength(IntelligenceLimits.MaxVisaTitle)
            .IsRequired();

        builder.Property(advisory => advisory.Summary)
            .HasMaxLength(IntelligenceLimits.MaxVisaSummary)
            .IsRequired();

        builder.Property(advisory => advisory.Content)
            .HasMaxLength(IntelligenceLimits.MaxVisaContent);

        builder.Property(advisory => advisory.ProcessingTime)
            .HasMaxLength(IntelligenceLimits.MaxProcessingTime);

        builder.Property(advisory => advisory.ApplicationUrl)
            .HasMaxLength(IntelligenceLimits.MaxUrl);

        builder.Property(advisory => advisory.OfficialReference)
            .HasMaxLength(IntelligenceLimits.MaxReferenceNumber);

        builder.Property(advisory => advisory.SourceUrl)
            .HasMaxLength(IntelligenceLimits.MaxUrl);
    }

    private static void ConfigureLines(EntityTypeBuilder<VisaAdvisory> builder) =>
        builder.OwnsMany(advisory => advisory.Lines, line =>
        {
            line.ToTable(
                IntelligencePersistenceConstants.VisaLinesTable, GuidesModuleMetadata.Schema);

            line.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
                .IsRequired();

            line.Property(value => value.Text)
                .HasMaxLength(IntelligenceLimits.MaxVisaLine)
                .IsRequired();

            line.WithOwner().HasForeignKey("VisaAdvisoryId");

            line.HasKey("VisaAdvisoryId", nameof(VisaLine.Kind), nameof(VisaLine.Position));
        });

    private static void ConfigureRelatedGuides(EntityTypeBuilder<VisaAdvisory> builder) =>
        builder.OwnsMany(advisory => advisory.RelatedGuides, guide =>
        {
            guide.ToTable(
                IntelligencePersistenceConstants.VisaGuidesTable, GuidesModuleMetadata.Schema);

            // Caller-supplied. Without ValueGeneratedNever, EF believes it generates the value and
            // emits an UPDATE where an INSERT was meant — surfacing as a bogus concurrency conflict
            // on the second guide an editor links, and never as anything that reads like a mapping
            // fault. The platform has this on record.
            guide.Property(value => value.GuideId).ValueGeneratedNever();

            guide.WithOwner().HasForeignKey("VisaAdvisoryId");

            guide.HasKey("VisaAdvisoryId", nameof(VisaGuideReference.GuideId));

            // "Which advisories point at this guide" — what puts a visa banner on a guide page.
            guide.HasIndex(value => value.GuideId)
                .HasDatabaseName("ix_intel_visa_guides_guide");
        });

    private static void ConfigureLocalizations(EntityTypeBuilder<VisaAdvisory> builder) =>
        builder.OwnsMany(advisory => advisory.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.VisaLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Title)
                .HasMaxLength(IntelligenceLimits.MaxVisaTitle)
                .IsRequired();

            localization.Property(value => value.Summary)
                .HasMaxLength(IntelligenceLimits.MaxVisaSummary)
                .IsRequired();

            localization.Property(value => value.Content)
                .HasMaxLength(IntelligenceLimits.MaxVisaContent)
                .IsRequired();

            localization.WithOwner().HasForeignKey("VisaAdvisoryId");

            localization.HasKey("VisaAdvisoryId", nameof(VisaAdvisoryLocalization.Locale));
        });
}

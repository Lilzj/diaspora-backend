using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the official notice and the four collections it owns.
/// <para>
/// <b>Every owned collection declares its own key.</b> Left to convention EF invents a
/// store-generated shadow integer inside a composite primary key; PostgreSQL tolerates it and SQLite
/// refuses the insert with a not-null violation — but only once a row actually exists, so a suite that
/// reads before it writes stays green. B11 paid for that in four collections and the platform now
/// fails the build for it. All four here declare a natural composite key, which every one of them
/// has: a locale, a media identifier, a (kind, target) pair and a language code are each unique
/// within their notice.
/// </para>
/// <para>
/// <b>The indexes are shaped by one query.</b> Nearly every read of this table is "the current
/// notices for a country, most serious first", and the composite over country, status and severity is
/// that query. The rest exist for the sweep, the category guard and the authority guard — each of
/// which is a narrow lookup that would otherwise scan a table whose whole point is to grow forever.
/// </para>
/// <para>
/// Archiving is a soft delete, so the shared context applies the global query filter and
/// <c>SoftDeleteConverter</c> handles the owned rows — nothing here declares either, deliberately.
/// </para>
/// </summary>
public sealed class OfficialNoticeConfiguration : IEntityTypeConfiguration<OfficialNotice>
{
    public void Configure(EntityTypeBuilder<OfficialNotice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IntelligencePersistenceConstants.NoticesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(notice => notice.Id);

        builder.Property(notice => notice.Id)
            .HasConversion(GuideValueConverters.Id<OfficialNoticeId>());

        ConfigureSubject(builder);
        ConfigureContent(builder);
        ConfigureProvenance(builder);
        ConfigureLifecycle(builder);
        ConfigureIndexes(builder);

        ConfigureLocalizations(builder);
        ConfigureAttachments(builder);
        ConfigureReferences(builder);
        ConfigureLanguages(builder);

        builder.Ignore(notice => notice.DomainEvents);
        builder.Ignore(notice => notice.IsReaderVisible);
    }

    private static void ConfigureSubject(EntityTypeBuilder<OfficialNotice> builder)
    {
        builder.Property(notice => notice.CountryCode)
            .HasMaxLength(IntelligencePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(notice => notice.Region)
            .HasMaxLength(IntelligenceLimits.MaxRegion);

        builder.Property(notice => notice.CategoryKey)
            .HasMaxLength(IntelligenceLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(notice => notice.AuthorityId)
            .HasConversion(GuideValueConverters.Id<IssuingAuthorityId>())
            .IsRequired();
    }

    private static void ConfigureContent(EntityTypeBuilder<OfficialNotice> builder)
    {
        builder.Property(notice => notice.Title)
            .HasMaxLength(IntelligenceLimits.MaxNoticeTitle)
            .IsRequired();

        builder.Property(notice => notice.Summary)
            .HasMaxLength(IntelligenceLimits.MaxNoticeSummary)
            .IsRequired();

        builder.Property(notice => notice.Content)
            .HasMaxLength(IntelligenceLimits.MaxNoticeContent)
            .IsRequired();

        builder.Property(notice => notice.Status)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(notice => notice.Severity)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        // Denormalised so the default sort can order by severity. The enum is stored as text
        // because text is legible in a table somebody reads at three in the morning; ordering by
        // that text puts Informational above Critical. See the aggregate for why the two cannot
        // drift.
        builder.Property(notice => notice.SeverityRank).IsRequired();
    }

    private static void ConfigureProvenance(EntityTypeBuilder<OfficialNotice> builder)
    {
        builder.Property(notice => notice.ReferenceNumber)
            .HasMaxLength(IntelligenceLimits.MaxReferenceNumber);

        builder.Property(notice => notice.SourceUrl)
            .HasMaxLength(IntelligenceLimits.MaxUrl);

        builder.Property(notice => notice.VerificationState)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(notice => notice.VerifiedBy)
            .HasConversion(GuideValueConverters.Id<UserId>());

        builder.Property(notice => notice.VerificationNote)
            .HasMaxLength(IntelligenceLimits.MaxNote);

        builder.Property(notice => notice.LastReviewedBy)
            .HasConversion(GuideValueConverters.Id<UserId>());
    }

    private static void ConfigureLifecycle(EntityTypeBuilder<OfficialNotice> builder)
    {
        builder.Property(notice => notice.SupersededById)
            .HasConversion(GuideValueConverters.Id<OfficialNoticeId>());

        // The concurrency token. An intelligence desk is edited by a small team all at once during
        // exactly the week something is happening in a country, which is the week a lost update
        // matters most.
        builder.Property(notice => notice.Revision).IsConcurrencyToken();
    }

    /// <summary>
    /// The indexes, each earning its place against a query that actually runs.
    /// <para>
    /// <b>No index on <c>SupersededById</c>, deliberately.</b> "What did this notice replace" is
    /// answered by loading one row; "what replaced this one" is a reverse lookup no screen makes —
    /// a reader always arrives at the superseded notice and follows the pointer forward.
    /// </para>
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<OfficialNotice> builder)
    {
        // The listing every reader-facing screen makes. On the rank rather than the enum, because
        // this index has to serve the ORDER BY as well as the WHERE.
        builder.HasIndex(notice => new { notice.CountryCode, notice.Status, notice.SeverityRank })
            .HasDatabaseName("ix_intel_notices_country_status_severity");

        // The category page, and the guard that refuses to archive a category still in use.
        builder.HasIndex(notice => new { notice.CountryCode, notice.CategoryKey })
            .HasDatabaseName("ix_intel_notices_country_category");

        // The guard that refuses to archive an authority whose notices are live.
        builder.HasIndex(notice => new { notice.AuthorityId, notice.Status })
            .HasDatabaseName("ix_intel_notices_authority_status");

        // The retirement sweep: published, expired, not yet announced.
        builder.HasIndex(notice => new { notice.Status, notice.ExpiresAt, notice.ExpiredAt })
            .HasDatabaseName("ix_intel_notices_expiry_sweep");

        // The chronology, and the "what is new" band.
        builder.HasIndex(notice => notice.PublishedAt)
            .HasDatabaseName("ix_intel_notices_published");
    }

    private static void ConfigureLocalizations(EntityTypeBuilder<OfficialNotice> builder) =>
        builder.OwnsMany(notice => notice.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.NoticeLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Title)
                .HasMaxLength(IntelligenceLimits.MaxNoticeTitle)
                .IsRequired();

            localization.Property(value => value.Summary)
                .HasMaxLength(IntelligenceLimits.MaxNoticeSummary)
                .IsRequired();

            localization.Property(value => value.Content)
                .HasMaxLength(IntelligenceLimits.MaxNoticeContent)
                .IsRequired();

            localization.WithOwner().HasForeignKey("OfficialNoticeId");

            // The pair is the identity: the aggregate deduplicates by locale, so (notice, locale)
            // is naturally unique and no surrogate is needed.
            localization.HasKey("OfficialNoticeId", nameof(NoticeLocalization.Locale));
        });

    private static void ConfigureAttachments(EntityTypeBuilder<OfficialNotice> builder) =>
        builder.OwnsMany(notice => notice.Attachments, attachment =>
        {
            attachment.ToTable(
                IntelligencePersistenceConstants.NoticeAttachmentsTable,
                GuidesModuleMetadata.Schema);

            attachment.Property(value => value.Caption)
                .HasMaxLength(IntelligenceLimits.MaxReferenceLabel);

            // The caller supplies this. Without ValueGeneratedNever, EF believes it generates the
            // value and emits an UPDATE where an INSERT was meant — which surfaces as a bogus
            // concurrency conflict on the second attachment somebody adds, and never as anything
            // that reads like a mapping fault. The platform has this on record.
            attachment.Property(value => value.MediaId).ValueGeneratedNever();

            attachment.WithOwner().HasForeignKey("OfficialNoticeId");

            attachment.HasKey("OfficialNoticeId", nameof(NoticeAttachment.MediaId));

            // "Which notices use this asset" — asked whenever B5 is about to retire one.
            attachment.HasIndex(value => value.MediaId)
                .HasDatabaseName("ix_intel_notice_attachments_media");
        });

    private static void ConfigureReferences(EntityTypeBuilder<OfficialNotice> builder) =>
        builder.OwnsMany(notice => notice.References, reference =>
        {
            reference.ToTable(
                IntelligencePersistenceConstants.NoticeReferencesTable,
                GuidesModuleMetadata.Schema);

            reference.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
                .IsRequired();

            reference.Property(value => value.Label)
                .HasMaxLength(IntelligenceLimits.MaxReferenceLabel);

            // Caller-supplied, like every identifier in a key on this aggregate. See the attachment
            // configuration for what EF does without it.
            reference.Property(value => value.TargetId).ValueGeneratedNever();

            reference.WithOwner().HasForeignKey("OfficialNoticeId");

            reference.HasKey(
                "OfficialNoticeId",
                nameof(NoticeReference.Kind),
                nameof(NoticeReference.TargetId));

            // "Which notices mention this guide" — what puts an advisory banner on a guide page.
            reference.HasIndex(value => new { value.Kind, value.TargetId })
                .HasDatabaseName("ix_intel_notice_references_target");
        });

    private static void ConfigureLanguages(EntityTypeBuilder<OfficialNotice> builder) =>
        builder.OwnsMany(notice => notice.Languages, language =>
        {
            language.ToTable(
                IntelligencePersistenceConstants.NoticeLanguagesTable,
                GuidesModuleMetadata.Schema);

            language.Property(value => value.Code)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            language.WithOwner().HasForeignKey("OfficialNoticeId");

            language.HasKey("OfficialNoticeId", nameof(NoticeLanguage.Code));
        });
}

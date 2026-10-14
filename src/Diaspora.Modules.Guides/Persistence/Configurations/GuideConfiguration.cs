using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the guide aggregate and everything it owns — seven tables.
/// <para>
/// <b>Every owned reference except the summary is declared required, and their scalars are
/// nullable where the domain says so.</b> That combination is the only EF Core shape that works
/// for table-sharing dependents, per B6's lesson and B7's five-fold repeat of it. The summary is
/// the one genuinely optional owned reference — the author may simply not have written one —
/// and it carries no nested dependents, which is what makes an optional all-nullable dependent
/// merely a round-trip caveat (all-null materialises as null) rather than a model-build failure.
/// </para>
/// <para>
/// <b>No index spans an owned type's columns and the aggregate's own.</b> EF cannot build one —
/// B6's lesson, recorded again by B7 — so indexes on owned columns (slug, country, language)
/// are declared inside their <c>OwnsOne</c> builders and indexes on aggregate columns outside.
/// Where a query genuinely needs both sides, the key is denormalised instead:
/// <c>GuideVote.VoterKey</c> exists for exactly that reason.
/// </para>
/// <para>
/// <b>Soft delete plus owned types is the combination B1.0.1 exists for.</b> This aggregate is
/// both — five owned collections, one nested two deep — and <c>SoftDeleteConverter</c> handles
/// it; nothing here has to work around it.
/// </para>
/// </summary>
public sealed class GuideConfiguration : IEntityTypeConfiguration<Guide>
{
    public void Configure(EntityTypeBuilder<Guide> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(GuidePersistenceConstants.GuidesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(guide => guide.Id);

        builder.Property(guide => guide.Id)
            .HasConversion(GuideValueConverters.Id<GuideId>());

        builder.Property(guide => guide.AuthorId)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(guide => guide.CategoryKey)
            .HasMaxLength(GuideLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(guide => guide.Latitude).HasColumnName("latitude");
        builder.Property(guide => guide.Longitude).HasColumnName("longitude");

        builder.Property(guide => guide.Status)
            .HasConversion<string>()
            .HasMaxLength(GuidePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(guide => guide.Visibility)
            .HasConversion<string>()
            .HasMaxLength(GuidePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(guide => guide.ModerationState)
            .HasConversion<string>()
            .HasMaxLength(GuidePersistenceConstants.EnumLength)
            .IsRequired();

        // The optimistic concurrency token. The aggregate bumps it on every content mutation
        // and EnsureRevision rejects a stale editor before the database ever has to; the token
        // is the backstop for the write that raced anyway.
        builder.Property(guide => guide.Revision).IsConcurrencyToken();

        ConfigureTitle(builder);
        ConfigureSlug(builder);
        ConfigureSummary(builder);
        ConfigureLanguage(builder);
        ConfigureLocation(builder);
        ConfigureSeo(builder);
        ConfigureReading(builder);
        ConfigureTrustMark(builder);
        ConfigureEngagement(builder);

        ConfigureSections(builder);
        ConfigureTags(builder);
        ConfigureMediaReferences(builder);
        ConfigureProviderReferences(builder);
        ConfigureModerationNotes(builder);

        ConfigureIndexes(builder);

        builder.Ignore(guide => guide.DomainEvents);
        builder.Ignore(guide => guide.OwnerId);
        builder.Ignore(guide => guide.IsPublishable);
        builder.Ignore(guide => guide.IsDiscoverable);
        builder.Ignore(guide => guide.WordCount);
    }

    private static void ConfigureTitle(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(GuideLimits.MaxTitle)
                .IsRequired();
        });

        builder.Navigation(guide => guide.Title).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(GuideLimits.MaxSlugLength)
                .IsRequired();

            // Unique, because it is the deep-link key: a duplicate makes one of two guides
            // permanently unreachable by link. It is also the cursor tie-break — the primary
            // key is a value-converted GuideId, which EF cannot compare in SQL.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_guides_slug");
        });

        builder.Navigation(guide => guide.Slug).IsRequired();
    }

    /// <summary>
    /// The one genuinely optional owned reference on the aggregate. Both scalars are nullable
    /// and there is no identifying column, so a summary whose fields are all null round-trips
    /// as a null navigation — which is exactly what it means: the author wrote nothing.
    /// </summary>
    private static void ConfigureSummary(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsOne(guide => guide.Summary, summary =>
        {
            summary.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(GuideLimits.MaxSummary);

            summary.Property(value => value.Excerpt)
                .HasColumnName("excerpt")
                .HasMaxLength(GuideLimits.MaxExcerpt);
        });

    private static void ConfigureLanguage(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Language, language =>
        {
            language.Property(value => value.Code)
                .HasColumnName("language")
                .HasMaxLength(GuidePersistenceConstants.LanguageCodeLength)
                .IsRequired();

            // "Guides in French" is a browse filter, and the column is owned — so the index
            // lives here, inside the owned builder, where EF can actually build it.
            language.HasIndex(value => value.Code).HasDatabaseName("ix_guides_language");
        });

        builder.Navigation(guide => guide.Language).IsRequired();
    }

    private static void ConfigureLocation(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(GuidePersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.RegionName)
                .HasColumnName("region_name")
                .HasMaxLength(GuideLimits.MaxLocationName);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(GuideLimits.MaxLocationName);

            location.Property(value => value.AreaId).HasColumnName("area_id");

            // A plain string, prefix-matchable with LIKE 'prefix%' — B4's EF lesson. The
            // repository's scope filter runs StartsWith against this column.
            location.Property(value => value.AreaPath)
                .HasColumnName("area_path")
                .HasMaxLength(GuideLimits.MaxAreaPath);

            // "Guides for the UK" is the single most common browse on the platform, and the
            // column is owned — so the index is declared here rather than on the aggregate.
            location.HasIndex(value => value.CountryCode).HasDatabaseName("ix_guides_country");
        });

        builder.Navigation(guide => guide.Location).IsRequired();
    }

    private static void ConfigureSeo(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Seo, seo =>
        {
            seo.Property(value => value.MetaTitle)
                .HasColumnName("meta_title")
                .HasMaxLength(GuideLimits.MaxSeoTitle);

            seo.Property(value => value.MetaDescription)
                .HasColumnName("meta_description")
                .HasMaxLength(GuideLimits.MaxSeoDescription);

            seo.Property(value => value.CanonicalUrl)
                .HasColumnName("canonical_url")
                .HasMaxLength(GuideLimits.MaxSeoUrl);

            seo.Property(value => value.Keywords)
                .HasColumnName("seo_keywords")
                .HasMaxLength(GuideLimits.MaxSeoKeywords);
        });

        // Required with all-nullable scalars: the navigation is always present so the read
        // path never branches on a missing object, exactly as the value object documents.
        builder.Navigation(guide => guide.Seo).IsRequired();
    }

    private static void ConfigureReading(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Reading, reading =>
        {
            reading.Property(value => value.Difficulty)
                .HasColumnName("difficulty")
                .HasConversion<string>()
                .HasMaxLength(GuidePersistenceConstants.EnumLength)
                .IsRequired();

            reading.Property(value => value.ReadingTimeMinutes)
                .HasColumnName("reading_time_minutes");

            reading.Property(value => value.EstimatedCompletionMinutes)
                .HasColumnName("estimated_completion_minutes");
        });

        builder.Navigation(guide => guide.Reading).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(GuidePersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(GuidePersistenceConstants.BadgeLength);

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(GuidePersistenceConstants.ReasonLength);

            mark.Property(value => value.VerifiedAt).HasColumnName("verified_at");

            // The staleness guard for at-least-once, unordered Trust deliveries. Persisted,
            // because the guard is only as durable as the timestamp it compares against.
            mark.Property(value => value.ChangedAt).HasColumnName("verification_changed_at");

            // The client's "verified guides only" toggle, and the column is owned — declared
            // here for the same reason as the country index.
            mark.HasIndex(value => value.State).HasDatabaseName("ix_guides_verification_state");
        });

        builder.Navigation(guide => guide.TrustMark).IsRequired();
    }

    private static void ConfigureEngagement(EntityTypeBuilder<Guide> builder)
    {
        builder.OwnsOne(guide => guide.Engagement, engagement =>
        {
            engagement.Property(value => value.Views).HasColumnName("view_count");
            engagement.Property(value => value.UniqueViews).HasColumnName("unique_view_count");
            engagement.Property(value => value.HelpfulCount).HasColumnName("helpful_count");
            engagement.Property(value => value.NotHelpfulCount).HasColumnName("not_helpful_count");
            engagement.Property(value => value.BookmarkCount).HasColumnName("bookmark_count");
            engagement.Property(value => value.ShareCount).HasColumnName("share_count");
            engagement.Property(value => value.CommentCount).HasColumnName("comment_count");
        });

        builder.Navigation(guide => guide.Engagement).IsRequired();
    }

    private static void ConfigureSections(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsMany(guide => guide.Sections, section =>
        {
            section.ToTable(
                GuidePersistenceConstants.GuideSectionsTable, GuidesModuleMetadata.Schema);

            // The identifier is editor-supplied and stable across saves — it is what version
            // diffs match sections by — so it is the key, not a surrogate.
            //
            // ValueGeneratedNever is load-bearing, not tidiness. EF's convention makes a Guid
            // key store-generated, and a store-generated key that already holds a non-default
            // value tells EF the row exists: it emits an UPDATE instead of an INSERT, matches
            // nothing, and surfaces as a concurrency conflict on the author's first save.
            section.HasKey(value => value.Id);
            section.Property(value => value.Id).ValueGeneratedNever();

            section.Property(value => value.Title)
                .HasMaxLength(GuideLimits.MaxSectionTitle)
                .IsRequired();

            section.OwnsMany(value => value.Blocks, block =>
            {
                block.ToTable(
                    GuidePersistenceConstants.GuideSectionBlocksTable,
                    GuidesModuleMetadata.Schema);

                block.HasKey(inner => inner.Id);
                block.Property(inner => inner.Id).ValueGeneratedNever();

                block.Property(inner => inner.Kind)
                    .HasConversion<string>()
                    .HasMaxLength(GuidePersistenceConstants.EnumLength)
                    .IsRequired();

                block.Property(inner => inner.Content)
                    .HasMaxLength(GuideLimits.MaxBlockLength);

                // Opaque JSON the editor owns. Unbounded, because bounding it here would be a
                // second, silent limit the domain never enforces.
                block.Property(inner => inner.Metadata);

                block.WithOwner().HasForeignKey("SectionId");
                block.Ignore(inner => inner.HasContent);
                block.Ignore(inner => inner.IsMediaKind);
                block.Ignore(inner => inner.WordCount);
                block.Ignore(inner => inner.DomainEvents);
            });

            section.WithOwner().HasForeignKey("GuideId");
            section.Ignore(value => value.HasContent);
            section.Ignore(value => value.WordCount);
            section.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureTags(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsMany(guide => guide.Tags, tag =>
        {
            tag.ToTable(GuidePersistenceConstants.GuideTagsTable, GuidesModuleMetadata.Schema);

            tag.Property(value => value.Slug)
                .HasMaxLength(GuideLimits.MaxTagLength)
                .IsRequired();

            tag.WithOwner().HasForeignKey("GuideId");

            // The pair is the identity: SetTags deduplicates by slug, so (guide, slug) is
            // naturally unique and no surrogate is needed.
            tag.HasKey("GuideId", nameof(GuideTagAssignment.Slug));

            // "bank-account" is a chip members tap. Indexed on the slug.
            tag.HasIndex(value => value.Slug).HasDatabaseName("ix_guide_tags_slug");
        });

    private static void ConfigureMediaReferences(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsMany(guide => guide.MediaReferences, reference =>
        {
            reference.ToTable(
                GuidePersistenceConstants.GuideMediaReferencesTable, GuidesModuleMetadata.Schema);

            reference.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(GuidePersistenceConstants.EnumLength)
                .IsRequired();

            reference.Property(value => value.Caption)
                .HasMaxLength(GuideLimits.MaxMediaCaption);

            reference.WithOwner().HasForeignKey("GuideId");

            // (media, role) is the domain identity — attaching the same asset in the same role
            // twice is a no-op — so the composite key states it and the database enforces it.
            reference.HasKey(
                "GuideId",
                nameof(GuideMediaReference.MediaId),
                nameof(GuideMediaReference.Role));

            // B5's unclaimed-asset bookkeeping resolves references by asset.
            reference.HasIndex(value => value.MediaId)
                .HasDatabaseName("ix_guide_media_references_media");
        });

    private static void ConfigureProviderReferences(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsMany(guide => guide.ProviderReferences, reference =>
        {
            reference.ToTable(
                GuidePersistenceConstants.GuideProviderReferencesTable,
                GuidesModuleMetadata.Schema);

            reference.Property(value => value.Role)
                .HasMaxLength(GuideLimits.MaxProviderReferenceRole);

            reference.Property(value => value.Note)
                .HasMaxLength(GuideLimits.MaxProviderReferenceNote);

            reference.WithOwner().HasForeignKey("GuideId");

            // Idempotent on the provider id, so the pair is the key.
            reference.HasKey("GuideId", nameof(GuideProviderReference.ProviderId));

            // The identifier half of this key is supplied by the caller, and EF marks a Guid key
            // property store-generated by convention. Left alone, EF reads a set key as "this row
            // already exists" and emits an UPDATE where an INSERT was meant, which surfaces as a
            // bogus concurrency conflict rather than a missing row. Twenty owned collections on this
            // platform already declare this; eight did not, until an architecture rule counted them.
            reference.Property(value => value.ProviderId).ValueGeneratedNever();

            // "Which guides recommend this provider" — the reverse lookup B7's detail page
            // will eventually ask for.
            reference.HasIndex(value => value.ProviderId)
                .HasDatabaseName("ix_guide_provider_references_provider");
        });

    private static void ConfigureModerationNotes(EntityTypeBuilder<Guide> builder) =>
        builder.OwnsMany(guide => guide.ModerationNotes, note =>
        {
            note.ToTable(
                GuidePersistenceConstants.GuideModerationNotesTable, GuidesModuleMetadata.Schema);

            note.HasKey(value => value.NoteId);

            note.Property(value => value.AuthorId)
                .HasConversion(GuideValueConverters.Id<UserId>())
                .IsRequired();

            note.Property(value => value.Note)
                .HasMaxLength(GuideLimits.MaxModerationNote)
                .IsRequired();

            note.WithOwner().HasForeignKey("GuideId");
        });

    private static void ConfigureIndexes(EntityTypeBuilder<Guide> builder)
    {
        // The pair every discovery query filters and orders on.
        builder.HasIndex(guide => new { guide.Status, guide.PublishedAt })
            .HasDatabaseName("ix_guides_status_published_at");

        // The "my guides" screen, and the per-author ceiling check on every create.
        builder.HasIndex(guide => guide.AuthorId)
            .HasDatabaseName("ix_guides_author");

        builder.HasIndex(guide => guide.CategoryKey)
            .HasDatabaseName("ix_guides_category");

        // Guides sharing the group are translations of each other; listing them is one scan.
        builder.HasIndex(guide => guide.TranslationGroupId)
            .HasDatabaseName("ix_guides_translation_group");

        // The scheduled publisher's poll: drafts whose instant has passed.
        builder.HasIndex(guide => guide.ScheduledPublishAt)
            .HasDatabaseName("ix_guides_scheduled");

        // The bounding-box prefilter's index, as on providers. A composite on both axes, because a
        // query that narrows latitude and then filters longitude in memory reads a band around
        // the whole planet.
        builder.HasIndex(guide => new { guide.Latitude, guide.Longitude })
            .HasDatabaseName("ix_guides_coordinates");
    }
}

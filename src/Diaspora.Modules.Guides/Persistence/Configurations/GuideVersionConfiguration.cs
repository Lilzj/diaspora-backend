using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps version snapshots.
/// <para>
/// <b>The unique index on (guide, version number) is the invariant.</b> Two rows claiming to be
/// version 3 of the same guide would make "what did readers see" unanswerable, and the database
/// enforces it rather than a handler because check-then-insert is a race by construction — two
/// concurrent publications of the same guide is exactly the shape that triggers it.
/// </para>
/// <para>
/// The content snapshot is unbounded text: the serializer owns its shape, versions are written
/// once and read rarely, and a length cap here would be a second, silent limit the domain never
/// enforces.
/// </para>
/// </summary>
public sealed class GuideVersionConfiguration : IEntityTypeConfiguration<GuideVersion>
{
    public void Configure(EntityTypeBuilder<GuideVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideVersionsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(version => version.Id);

        builder.Property(version => version.Id)
            .HasConversion(GuideValueConverters.Id<GuideVersionId>());

        builder.Property(version => version.GuideId)
            .HasConversion(GuideValueConverters.Id<GuideId>())
            .IsRequired();

        builder.Property(version => version.EditorId)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(version => version.Title)
            .HasMaxLength(GuideLimits.MaxTitle)
            .IsRequired();

        builder.Property(version => version.Summary)
            .HasMaxLength(GuideLimits.MaxSummary);

        builder.Property(version => version.Language)
            .HasMaxLength(GuidePersistenceConstants.LanguageCodeLength)
            .IsRequired();

        builder.Property(version => version.CategoryKey)
            .HasMaxLength(GuideLimits.MaxCategoryKey)
            .IsRequired();

        // Unbounded: the sections as JSON, in the snapshot serializer's shape.
        builder.Property(version => version.ContentSnapshot).IsRequired();

        builder.Property(version => version.RevisionNotes)
            .HasMaxLength(GuideLimits.MaxRevisionNotes);

        builder.HasIndex(version => new { version.GuideId, version.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_guide_versions_guide_version");

        builder.Ignore(version => version.DomainEvents);
    }
}

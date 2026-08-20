using Diaspora.Modules.Guides.Domain.Drafts;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps auto-save snapshots.
/// <para>
/// The (guide, saved-at) index serves the only two queries this table ever answers: "the newest
/// snapshots for this guide" on recovery, and "everything past the newest ten" on the prune
/// that follows every save. The snapshot itself is unbounded text in the serializer's shape.
/// </para>
/// </summary>
public sealed class GuideDraftSnapshotConfiguration : IEntityTypeConfiguration<GuideDraftSnapshot>
{
    public void Configure(EntityTypeBuilder<GuideDraftSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideDraftSnapshotsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id)
            .HasConversion(GuideValueConverters.Id<GuideDraftSnapshotId>());

        builder.Property(snapshot => snapshot.GuideId)
            .HasConversion(GuideValueConverters.Id<GuideId>())
            .IsRequired();

        builder.Property(snapshot => snapshot.SavedBy)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        // Unbounded: the draft as JSON, in the snapshot serializer's shape.
        builder.Property(snapshot => snapshot.Snapshot).IsRequired();

        builder.HasIndex(snapshot => new { snapshot.GuideId, snapshot.SavedAt })
            .HasDatabaseName("ix_guide_drafts_guide_saved");

        builder.Ignore(snapshot => snapshot.DomainEvents);
    }
}

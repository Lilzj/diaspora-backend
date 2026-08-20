using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Bookmarks.Persistence;

/// <summary>
/// Maps the <see cref="Bookmark"/> aggregate into the module's own schema.
/// <para>
/// The <c>BookmarkId</c> converter is registered here rather than inherited from the
/// platform. B1's convention-level converter scans only the SharedKernel assembly for
/// strongly typed ids, so a module that declares its own id in its own assembly is
/// responsible for its own conversion — which is the correct ownership line, and cheap
/// with the reusable <see cref="StronglyTypedIdValueConverter{TId}"/>.
/// </para>
/// </summary>
public sealed class BookmarkEntityConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bookmarks", BookmarksModuleMetadata.Schema);

        builder.HasKey(bookmark => bookmark.Id);

        builder.Property(bookmark => bookmark.Id)
            .HasConversion(new StronglyTypedIdValueConverter<BookmarkId>());

        builder.Property(bookmark => bookmark.UserId).IsRequired();

        builder.Property(bookmark => bookmark.TargetType)
            .HasMaxLength(Bookmark.MaxTargetTypeLength)
            .IsRequired();

        builder.Property(bookmark => bookmark.TargetId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(bookmark => bookmark.Note)
            .HasMaxLength(Bookmark.MaxNoteLength);

        builder.Property(bookmark => bookmark.CreatedAt).IsRequired();

        // One save per (member, thing). The unique index is the invariant made physical.
        builder.HasIndex(bookmark => new { bookmark.UserId, bookmark.TargetType, bookmark.TargetId })
            .IsUnique()
            .HasDatabaseName("ix_bookmarks_user_target");

        builder.Ignore(bookmark => bookmark.DomainEvents);
    }
}

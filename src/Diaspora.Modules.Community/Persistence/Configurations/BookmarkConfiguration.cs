using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps bookmarks. The composite key column is what makes one-save-per-member a real index.
/// </summary>
public sealed class BookmarkConfiguration : IEntityTypeConfiguration<CommunityBookmark>
{
    public void Configure(EntityTypeBuilder<CommunityBookmark> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.BookmarksTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<CommunityBookmarkId>());

        builder.Property(entity => entity.UserId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(entity => entity.FolderId)
            .HasConversion(CommunityValueConverters.Id<BookmarkFolderId>());

        builder.Property(entity => entity.ContentKey)
            .HasMaxLength(CommunityPersistenceConstants.CompositeKeyLength)
            .IsRequired();

        builder.Property(entity => entity.Note)
            .HasMaxLength(CommunityBookmark.MaxNoteLength);

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_bookmarks_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.HasIndex(entity => entity.ContentKey)
            .IsUnique()
            .HasDatabaseName("ix_bookmarks_user_content");

        builder.HasIndex(entity => new { entity.UserId, entity.SavedAt })
            .HasDatabaseName("ix_bookmarks_user_saved");

        builder.Ignore(entity => entity.DomainEvents);
    }
}

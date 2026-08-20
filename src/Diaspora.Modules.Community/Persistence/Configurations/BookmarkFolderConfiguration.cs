using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps member-named bookmark folders.</summary>
public sealed class BookmarkFolderConfiguration : IEntityTypeConfiguration<BookmarkFolder>
{
    public void Configure(EntityTypeBuilder<BookmarkFolder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.BookmarkFoldersTable, CommunityModuleMetadata.Schema);

        builder.HasKey(folder => folder.Id);

        builder.Property(folder => folder.Id)
            .HasConversion(CommunityValueConverters.Id<BookmarkFolderId>());

        builder.Property(folder => folder.OwnerId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(folder => folder.Name)
            .HasMaxLength(BookmarkFolder.MaxNameLength)
            .IsRequired();

        builder.Property(folder => folder.Glyph).HasMaxLength(16);

        builder.HasIndex(folder => folder.OwnerId)
            .HasDatabaseName("ix_bookmark_folders_owner");

        builder.Ignore(folder => folder.DomainEvents);
    }
}

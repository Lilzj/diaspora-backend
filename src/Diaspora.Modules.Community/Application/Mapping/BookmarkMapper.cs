using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>Projects bookmarks, their type-derived collections and their named folders.</summary>
public static class BookmarkMapper
{
    /// <summary>The glyphs the client already renders beside each saved collection.</summary>
    private static readonly Dictionary<CommunityContentType, string> Glyphs = new()
    {
        [CommunityContentType.Listing] = "\U0001F4CD",
        [CommunityContentType.Guide] = "\U0001F4D6",
        [CommunityContentType.Post] = "\U0001F4E2",
        [CommunityContentType.Job] = "\U0001F4BC",
        [CommunityContentType.Housing] = "\U0001F3E0",
        [CommunityContentType.Marketplace] = "\U0001F6D2",
        [CommunityContentType.Event] = "\U0001F389",
        [CommunityContentType.Organization] = "\U0001F3DB"
    };

    public static BookmarkResponse ToResponse(CommunityBookmark bookmark, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        return new BookmarkResponse
        {
            BookmarkId = bookmark.Id.Value,
            SavedId = bookmark.Id.Value,
            UserId = bookmark.UserId.Value,
            ContentType = CommunityWireNames.ToWire(bookmark.Content.Type),
            ContentId = bookmark.Content.Id,
            FolderId = bookmark.FolderId?.Value,
            Note = bookmark.Note,
            Title = title,
            CreatedAt = bookmark.SavedAt
        };
    }

    public static BookmarkFolderResponse ToResponse(BookmarkFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        return new BookmarkFolderResponse
        {
            FolderId = folder.Id.Value,
            Name = folder.Name,
            Glyph = folder.Glyph,
            ItemCount = folder.ItemCount,
            Position = folder.Position,
            CreatedAt = folder.CreatedAt
        };
    }

    /// <summary>
    /// The type-derived tiles.
    /// <para>
    /// Every supported type is returned, including the ones with a count of zero, because the
    /// client's saved screen renders an empty tile rather than hiding the category — a member
    /// who has saved no jobs should still see that jobs can be saved.
    /// </para>
    /// </summary>
    public static IReadOnlyList<BookmarkCollectionResponse> ToCollections(
        IReadOnlyDictionary<CommunityContentType, int> counts,
        IReadOnlyCollection<CommunityContentType> supported) =>
        supported
            .Where(type => type != CommunityContentType.Comment)
            .Select(type => new BookmarkCollectionResponse
            {
                ContentType = CommunityWireNames.ToWire(type),
                FolderId = null,
                Label = CommunityWireNames.ContentTypeLabel(type),
                Icon = Glyphs.GetValueOrDefault(type),
                Count = counts.GetValueOrDefault(type),
                IsAvailable = true,
                ThumbnailMediaIds = []
            })
            .ToList();
}

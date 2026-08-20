using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>
/// Builds the saved screen.
/// <para>
/// Type tiles first, then named folders, in one list — because the client renders them as one
/// grid and asking it to merge two responses would put the ordering rule on the client.
/// </para>
/// </summary>
public sealed class ListBookmarkCollectionsQueryHandler
    : IQueryHandler<ListBookmarkCollectionsQuery, IReadOnlyList<BookmarkCollectionResponse>>
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;

    public ListBookmarkCollectionsQueryHandler(
        IBookmarkRepository bookmarks,
        ContentReferenceResolver references,
        ICurrentUser currentUser)
    {
        _bookmarks = bookmarks;
        _references = references;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<BookmarkCollectionResponse>>> Handle(
        ListBookmarkCollectionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<BookmarkCollectionResponse>>(
                CommunityErrors.NotAuthenticated);
        }

        var counts = await _bookmarks.CountByTypeAsync(userId, cancellationToken);
        var folders = await _bookmarks.ListFoldersAsync(userId, cancellationToken);

        var collections = BookmarkMapper
            .ToCollections(counts, _references.SupportedTypes)
            .ToList();

        collections.AddRange(folders.Select(folder => new BookmarkCollectionResponse
        {
            ContentType = null,
            FolderId = folder.Id.Value,
            Label = folder.Name,
            Icon = folder.Glyph,
            Count = folder.ItemCount,
            IsAvailable = true,
            ThumbnailMediaIds = []
        }));

        return Result.Success<IReadOnlyList<BookmarkCollectionResponse>>(collections);
    }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Bookmarks.Contracts;
using Diaspora.Modules.Bookmarks.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Bookmarks.Application;

/// <summary>Lists the signed-in member's bookmarks. A query — never wrapped in a transaction.</summary>
[RequirePermission(BookmarkPermissions.Read)]
public sealed record ListBookmarksQuery : IQuery<IReadOnlyList<BookmarkResponse>>;

public sealed class ListBookmarksQueryHandler
    : IQueryHandler<ListBookmarksQuery, IReadOnlyList<BookmarkResponse>>
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICurrentUser _currentUser;

    public ListBookmarksQueryHandler(IBookmarkRepository bookmarks, ICurrentUser currentUser)
    {
        _bookmarks = bookmarks;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<BookmarkResponse>>> Handle(
        ListBookmarksQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<BookmarkResponse>>(BookmarkErrors.NotAuthenticated);
        }

        var bookmarks = await _bookmarks.ListByUserAsync(userId, cancellationToken);

        return Result.Success<IReadOnlyList<BookmarkResponse>>(
            bookmarks.Select(BookmarkMapper.ToResponse).ToArray());
    }
}

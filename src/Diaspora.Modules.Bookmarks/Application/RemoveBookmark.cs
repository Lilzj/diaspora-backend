using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Modules.Bookmarks.Observability;
using Diaspora.Modules.Bookmarks.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Bookmarks.Application;

/// <summary>Removes one of the signed-in member's bookmarks.</summary>
[RequirePermission(BookmarkPermissions.Manage)]
public sealed record RemoveBookmarkCommand : ICommand
{
    public required Guid BookmarkId { get; init; }
}

public sealed class RemoveBookmarkCommandHandler : ICommandHandler<RemoveBookmarkCommand>
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveBookmarkCommandHandler(
        IBookmarkRepository bookmarks,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _bookmarks = bookmarks;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RemoveBookmarkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure(BookmarkErrors.NotAuthenticated);
        }

        var bookmark = await _bookmarks.GetByIdAsync(Domain.BookmarkId.From(request.BookmarkId), cancellationToken);

        // The ownership check is the security boundary: a member may only remove their own.
        // Returning NotFound rather than Forbidden keeps one member from probing for the
        // existence of another's bookmarks.
        if (bookmark is null || bookmark.UserId != userId)
        {
            return Result.Failure(BookmarkErrors.NotFound);
        }

        _bookmarks.Remove(bookmark);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        BookmarkTelemetrySource.Removed.Add(1);

        return Result.Success();
    }
}

using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Creates a folder, refusing a duplicate name.</summary>
public sealed class CreateBookmarkFolderCommandHandler
    : ICommandHandler<CreateBookmarkFolderCommand, BookmarkFolderResponse>
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBookmarkFolderCommandHandler(
        IBookmarkRepository bookmarks, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _bookmarks = bookmarks;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BookmarkFolderResponse>> Handle(
        CreateBookmarkFolderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<BookmarkFolderResponse>(CommunityErrors.NotAuthenticated);
        }

        var existing = await _bookmarks.ListFoldersAsync(userId, cancellationToken);

        if (existing.Count >= CommunityLimits.MaxBookmarkFolders)
        {
            return Result.Failure<BookmarkFolderResponse>(
                CommunityErrors.BookmarkFolderLimit(CommunityLimits.MaxBookmarkFolders));
        }

        if (existing.Any(folder =>
                string.Equals(folder.Name, request.Request.Name?.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<BookmarkFolderResponse>(
                CommunityErrors.BookmarkFolderNameTaken);
        }

        var folder = BookmarkFolder.Create(
            userId, request.Request.Name, request.Request.Glyph, existing.Count);

        await _bookmarks.AddFolderAsync(folder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BookmarkMapper.ToResponse(folder));
    }
}

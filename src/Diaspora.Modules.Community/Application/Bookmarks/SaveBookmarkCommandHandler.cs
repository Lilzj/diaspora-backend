using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Saves content.</summary>
public sealed class SaveBookmarkCommandHandler
    : ICommandHandler<SaveBookmarkCommand, BookmarkResponse>
{
    private readonly BookmarkService _bookmarks;
    private readonly IBookmarkRepository _repository;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;

    public SaveBookmarkCommandHandler(
        BookmarkService bookmarks,
        IBookmarkRepository repository,
        ContentReferenceResolver references,
        ICurrentUser currentUser)
    {
        _bookmarks = bookmarks;
        _repository = repository;
        _references = references;
        _currentUser = currentUser;
    }

    public async Task<Result<BookmarkResponse>> Handle(
        SaveBookmarkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<BookmarkResponse>(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(
            request.Request.ContentType, request.Request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<BookmarkResponse>(reference.Error);
        }

        var saved = await _bookmarks.SaveAsync(
            reference.Value,
            userId,
            request.Request.FolderId is { } folderId ? BookmarkFolderId.From(folderId) : null,
            request.Request.Note,
            cancellationToken);

        if (saved.IsFailure)
        {
            return Result.Failure<BookmarkResponse>(saved.Error);
        }

        var bookmark = await _repository.FindAsync(userId, reference.Value, cancellationToken);

        return bookmark is null
            ? Result.Failure<BookmarkResponse>(CommunityErrors.BookmarkNotFound)
            : Result.Success(BookmarkMapper.ToResponse(bookmark));
    }
}

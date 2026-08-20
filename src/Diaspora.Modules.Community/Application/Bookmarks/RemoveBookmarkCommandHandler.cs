using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Unsaves content.</summary>
public sealed class RemoveBookmarkCommandHandler : ICommandHandler<RemoveBookmarkCommand>
{
    private readonly BookmarkService _bookmarks;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;

    public RemoveBookmarkCommandHandler(
        BookmarkService bookmarks,
        ContentReferenceResolver references,
        ICurrentUser currentUser)
    {
        _bookmarks = bookmarks;
        _references = references;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        RemoveBookmarkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        return reference.IsFailure
            ? Result.Failure(reference.Error)
            : await _bookmarks.UnsaveAsync(reference.Value, userId, cancellationToken);
    }
}

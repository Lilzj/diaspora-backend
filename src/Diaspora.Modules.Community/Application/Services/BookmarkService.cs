using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>Saves and unsaves anything, for any module.</summary>
public sealed class BookmarkService : ICommunityBookmarkService
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICommunityPostRepository _posts;
    private readonly ContentReferenceResolver _references;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public BookmarkService(
        IBookmarkRepository bookmarks,
        ICommunityPostRepository posts,
        ContentReferenceResolver references,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _bookmarks = bookmarks;
        _posts = posts;
        _references = references;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> SaveAsync(
        ContentReference content, UserId userId, CancellationToken cancellationToken = default) =>
        SaveAsync(content, userId, null, null, cancellationToken);

    public async Task<Result> SaveAsync(
        ContentReference content,
        UserId userId,
        BookmarkFolderId? folderId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await _bookmarks.FindAsync(userId, content, cancellationToken);

        if (existing is not null)
        {
            // Saving twice is not an error worth surfacing — it is a double tap. The folder and
            // note are still applied, because a second save from the folder picker is how a
            // member moves something.
            existing.MoveTo(folderId);
            existing.SetNote(note);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        var descriptor = await _references.DescribeAsync(content, cancellationToken);

        if (descriptor is null || !descriptor.Exists)
        {
            return Result.Failure(CommunityErrors.ContentNotFound);
        }

        if (folderId is not null)
        {
            var folder = await _bookmarks.GetFolderAsync(folderId, cancellationToken);

            if (folder is null || folder.OwnerId != userId)
            {
                return Result.Failure(CommunityErrors.BookmarkFolderNotFound);
            }

            folder.AdjustItems(1);
        }

        var bookmark = CommunityBookmark.Create(
            userId, content, _clock.UtcNow, folderId, note, descriptor.AuthorId);

        await _bookmarks.AddAsync(bookmark, cancellationToken);

        if (content.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);
            post?.ApplyEngagement(counters => counters.WithBookmarks(1));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnsaveAsync(
        ContentReference content, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await _bookmarks.FindAsync(userId, content, cancellationToken);

        if (existing is null)
        {
            return Result.Failure(CommunityErrors.BookmarkNotFound);
        }

        if (existing.FolderId is { } folderId)
        {
            var folder = await _bookmarks.GetFolderAsync(folderId, cancellationToken);
            folder?.AdjustItems(-1);
        }

        existing.RaiseRemoved();
        _bookmarks.Remove(existing);

        if (content.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);
            post?.ApplyEngagement(counters => counters.WithBookmarks(-1));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public Task<IReadOnlySet<string>> FilterSavedAsync(
        UserId userId,
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default) =>
        _bookmarks.FindSavedAsync(userId, contents, cancellationToken);
}

using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Adds and reads comments, on anything.
/// <para>
/// The service every comment path goes through — the HTTP endpoint, and any module consuming
/// <see cref="ICommunityCommentService"/> in-process. One implementation, so threading,
/// tombstoning, counter maintenance and the closed-thread rule behave identically no matter
/// which module the content belongs to.
/// </para>
/// </summary>
public sealed class CommentService : ICommunityCommentService
{
    private readonly ICommentRepository _comments;
    private readonly ICommunityPostRepository _posts;
    private readonly ContentReferenceResolver _references;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CommentService(
        ICommentRepository comments,
        ICommunityPostRepository posts,
        ContentReferenceResolver references,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _comments = comments;
        _posts = posts;
        _references = references;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Comment>> AddAsync(
        ContentReference content,
        UserId authorId,
        string body,
        CommentId? parentCommentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(authorId);

        var text = CommentBody.Create(body);

        if (text.IsFailure)
        {
            return Result.Failure<Comment>(text.Error);
        }

        var descriptor = await _references.DescribeAsync(content, cancellationToken);

        if (descriptor is null || !descriptor.Exists)
        {
            return Result.Failure<Comment>(CommunityErrors.ContentNotFound);
        }

        if (!descriptor.AcceptsComments)
        {
            return Result.Failure<Comment>(CommunityErrors.CommentsClosed);
        }

        Comment? parent = null;

        if (parentCommentId is not null)
        {
            parent = await _comments.GetAsync(parentCommentId, cancellationToken);

            if (parent is null)
            {
                return Result.Failure<Comment>(CommunityErrors.CommentNotFound);
            }
        }

        var created = Comment.Create(content, authorId, text.Value, parent, _clock.UtcNow);

        if (created.IsFailure)
        {
            return created;
        }

        var comment = created.Value;

        await _comments.AddAsync(comment, cancellationToken);

        parent?.AdjustReplies(1);

        // The thread count lives on the post because a feed card renders it. Content owned by
        // another module keeps its own count, or asks for one through the social summary.
        if (content.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);
            post?.ApplyEngagement(counters => counters.WithComments(1));
        }

        comment.RaiseAdded(descriptor.AuthorId, parent?.AuthorId.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(comment);
    }

    public async Task<CommentThread> GetThreadAsync(
        ContentReference content,
        int rootLimit,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var roots = await _comments.ListRootsAsync(
            content, null, rootLimit, includeHidden, cancellationToken);

        var replies = roots.Items.Count == 0
            ? []
            : await _comments.ListRepliesAsync(
                roots.Items.Select(comment => comment.Id).ToList(),
                CommunityLimits.MaxFeedPageSize,
                cancellationToken);

        return CommentThread.Assemble(content, [.. roots.Items, .. replies]);
    }

    public Task<int> CountAsync(
        ContentReference content, CancellationToken cancellationToken = default) =>
        _comments.CountAsync(content, cancellationToken);
}

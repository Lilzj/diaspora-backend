using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Deletes a comment and keeps its thread intact.</summary>
public sealed class DeleteCommentCommandHandler : ICommandHandler<DeleteCommentCommand>
{
    private readonly ICommentRepository _comments;
    private readonly ICommunityPostRepository _posts;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DeleteCommentCommandHandler(
        ICommentRepository comments,
        ICommunityPostRepository posts,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _comments = comments;
        _posts = posts;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(CommunityErrors.NotAuthenticated);
        }

        var comment = await _comments.GetAsync(
            CommentId.From(request.CommentId), cancellationToken);

        if (comment is null)
        {
            return Result.Failure(CommunityErrors.CommentNotFound);
        }

        if (comment.AuthorId != userId)
        {
            return Result.Failure(CommunityErrors.CommentNotOwned);
        }

        comment.MarkDeleted(_clock.UtcNow, userId.Value.ToString());

        if (comment.ParentCommentId is { } parentId)
        {
            var parent = await _comments.GetAsync(parentId, cancellationToken);
            parent?.AdjustReplies(-1);
        }

        if (comment.Content.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(
                CommunityPostId.From(comment.Content.Id), cancellationToken);

            post?.ApplyEngagement(counters => counters.WithComments(-1));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

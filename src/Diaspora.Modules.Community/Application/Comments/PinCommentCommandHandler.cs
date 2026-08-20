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

/// <summary>
/// Pins a comment.
/// <para>
/// The author of the content may pin within their own thread; a moderator may pin anywhere. The
/// post also records which comment is pinned, so a feed card can render it without loading the
/// thread.
/// </para>
/// </summary>
public sealed class PinCommentCommandHandler : ICommandHandler<PinCommentCommand>
{
    private readonly ICommentRepository _comments;
    private readonly ICommunityPostRepository _posts;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public PinCommentCommandHandler(
        ICommentRepository comments,
        ICommunityPostRepository posts,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _comments = comments;
        _posts = posts;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(PinCommentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comment = await _comments.GetAsync(
            CommentId.From(request.CommentId), cancellationToken);

        if (comment is null)
        {
            return Result.Failure(CommunityErrors.CommentNotFound);
        }

        comment.Pin(request.Pinned);

        if (comment.Content.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(
                CommunityPostId.From(comment.Content.Id), cancellationToken);

            post?.PinComment(request.Pinned ? comment.Id : null);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

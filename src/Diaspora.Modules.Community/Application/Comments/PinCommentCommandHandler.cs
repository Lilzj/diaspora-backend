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

        CommunityPost? post = null;

        if (comment.Content.Type == CommunityContentType.Post)
        {
            post = await _posts.GetAsync(
                CommunityPostId.From(comment.Content.Id), cancellationToken);
        }

        // The rule in the summary above, enforced since 6 Nov 2026; before then any verified
        // member could pin any comment anywhere. The owner of other kinds of content (a guide, an
        // event) is another module's to say, so on those only a moderator may pin.
        var ownsThread = post is not null && post.AuthorId == userId;
        var isModerator = _currentUser.Permissions.Contains(
            CommunityPermissions.Moderate, StringComparer.Ordinal);

        if (!ownsThread && !isModerator)
        {
            return Result.Failure(CommunityErrors.Forbidden);
        }

        comment.Pin(request.Pinned);
        post?.PinComment(request.Pinned ? comment.Id : null);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

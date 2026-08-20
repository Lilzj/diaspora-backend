using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>
/// Deletes a post.
/// <para>
/// Calls <c>MarkDeleted</c> on the aggregate rather than removing it from the change tracker,
/// which is what lets the delete raise its event and set a status. B1.0.1 fixed the soft-delete
/// interceptor that made this a workaround in B5; here it is the intended shape.
/// </para>
/// </summary>
public sealed class DeletePostCommandHandler : ICommandHandler<DeletePostCommand>
{
    private readonly ICommunityPostRepository _posts;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DeletePostCommandHandler(
        ICommunityPostRepository posts,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _posts = posts;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(CommunityErrors.NotAuthenticated);
        }

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure(CommunityErrors.PostNotFound);
        }

        if (post.AuthorId != userId)
        {
            return Result.Failure(CommunityErrors.PostNotOwned);
        }

        post.MarkDeleted(_clock.UtcNow, userId.Value.ToString());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>
/// Withdraws or cancels a post.
/// <para>
/// Both leave the post readable and marked, rather than removing it, because a member who changed
/// their route for a road closure needs to find out it was withdrawn — not discover that the alert
/// they acted on no longer exists.
/// </para>
/// </summary>
public sealed class ClosePostCommandHandler : ICommandHandler<ClosePostCommand, CommunityPostResponse>
{
    private readonly ICommunityPostRepository _posts;
    private readonly ViewerContextResolver _viewers;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly CommunityAuthorNames _authorNames;

    public ClosePostCommandHandler(
        ICommunityPostRepository posts,
        ViewerContextResolver viewers,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock,
        CommunityAuthorNames authorNames)
    {
        _posts = posts;
        _viewers = viewers;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _authorNames = authorNames;
    }

    public async Task<Result<CommunityPostResponse>> Handle(
        ClosePostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not { } userId)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.NotAuthenticated);
        }

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.PostNotFound);
        }

        var closed = request.Closure == PostClosure.Withdraw
            ? post.Withdraw(userId, _clock.UtcNow)
            : post.Cancel(userId, _clock.UtcNow);

        if (closed.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(closed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var names = await _authorNames.ResolveAsync([post.AuthorId], cancellationToken);

        return Result.Success(PostMapper.ToResponse(post, null, viewer, names: names));
    }
}

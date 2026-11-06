using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>
/// Pins a post.
/// <para>
/// A pinned post leads a feed everybody reads, so pinning is a moderator's call, not the
/// author's. Until 6 Nov 2026 this handler checked nobody: <c>community:pin</c> is held by every
/// verified member, so any of them could pin or unpin anyone's post.
/// </para>
/// </summary>
public sealed class PinPostCommandHandler : ICommandHandler<PinPostCommand>
{
    private readonly ICommunityPostRepository _posts;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public PinPostCommandHandler(
        ICommunityPostRepository posts,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _posts = posts;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(PinPostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is null)
        {
            return Result.Failure(CommunityErrors.NotAuthenticated);
        }

        // Refused before the post is read, so a refusal says nothing about whether it exists.
        if (!_currentUser.Permissions.Contains(CommunityPermissions.Moderate, StringComparer.Ordinal))
        {
            return Result.Failure(CommunityErrors.Forbidden);
        }

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure(CommunityErrors.PostNotFound);
        }

        post.Pin(request.Pinned);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

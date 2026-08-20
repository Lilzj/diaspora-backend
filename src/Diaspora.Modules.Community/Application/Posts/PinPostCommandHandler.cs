using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Pins a post.</summary>
public sealed class PinPostCommandHandler : ICommandHandler<PinPostCommand>
{
    private readonly ICommunityPostRepository _posts;
    private readonly IUnitOfWork _unitOfWork;

    public PinPostCommandHandler(ICommunityPostRepository posts, IUnitOfWork unitOfWork)
    {
        _posts = posts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(PinPostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

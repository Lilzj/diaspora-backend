using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Edits a post, re-extracting its tags and mentions from the new body.</summary>
public sealed class UpdatePostCommandHandler
    : ICommandHandler<UpdatePostCommand, CommunityPostResponse>
{
    private readonly ICommunityPostRepository _posts;
    private readonly PostComposer _composer;
    private readonly ViewerContextResolver _viewers;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdatePostCommandHandler(
        ICommunityPostRepository posts,
        PostComposer composer,
        ViewerContextResolver viewers,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _posts = posts;
        _composer = composer;
        _viewers = viewers;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CommunityPostResponse>> Handle(
        UpdatePostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.NotAuthenticated);
        }

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.PostNotFound);
        }

        if (post.AuthorId != userId)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.PostNotOwned);
        }

        var body = PostBody.Create(request.Request.Body);

        if (body.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(body.Error);
        }

        var title = PostTitle.CreateOptional(request.Request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(title.Error);
        }

        var edited = post.Edit(
            body.Value, title.Value, userId, request.Request.ChangeSummary, _clock.UtcNow);

        if (edited.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(edited.Error);
        }

        if (request.Request.Attachments is not null)
        {
            foreach (var existing in post.Attachments.ToList())
            {
                post.Detach(existing.Id);
            }

            var applied = PostComposer.ApplyAttachments(post, request.Request.Attachments);

            if (applied.IsFailure)
            {
                return Result.Failure<CommunityPostResponse>(applied.Error);
            }
        }

        await _composer.ProcessBodyAsync(post, userId, isNew: false, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        return Result.Success(PostMapper.ToResponse(post, null, viewer));
    }
}

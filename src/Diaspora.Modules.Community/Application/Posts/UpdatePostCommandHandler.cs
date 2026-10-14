using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
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
    private readonly CommunityAuthorNames _authorNames;

    public UpdatePostCommandHandler(
        ICommunityPostRepository posts,
        PostComposer composer,
        ViewerContextResolver viewers,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock,
        CommunityAuthorNames authorNames)
    {
        _posts = posts;
        _composer = composer;
        _viewers = viewers;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _authorNames = authorNames;
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

        var payload = request.Request;

        if (payload.Urgency is not null || payload.AffectedRadiusMetres is not null)
        {
            UrgencyLevel? urgency = payload.Urgency is null
                ? null
                : CommunityWireNames.ParseOr(payload.Urgency, post.Urgency);

            // Raising an alert to Emergency is publishing one, and needs the same permission.
            // Keeping an alert that is already Emergency at Emergency does not.
            if (urgency == UrgencyLevel.Emergency
                && post.Urgency != UrgencyLevel.Emergency
                && !_currentUser.Permissions.Contains(
                    CommunityPermissions.PublishEmergency, StringComparer.Ordinal))
            {
                return Result.Failure<CommunityPostResponse>(CommunityErrors.EmergencyNotPermitted);
            }

            var amended = post.AmendAlert(
                urgency, payload.AffectedRadiusMetres, userId, _clock.UtcNow);

            if (amended.IsFailure)
            {
                return Result.Failure<CommunityPostResponse>(amended.Error);
            }
        }

        await _composer.ProcessBodyAsync(post, userId, isNew: false, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var names = await _authorNames.ResolveAsync([post.AuthorId], cancellationToken);

        return Result.Success(PostMapper.ToResponse(post, null, viewer, names: names));
    }
}

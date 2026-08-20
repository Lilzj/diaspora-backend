using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Edits a comment.</summary>
public sealed class UpdateCommentCommandHandler
    : ICommandHandler<UpdateCommentCommand, CommentResponse>
{
    private readonly ICommentRepository _comments;
    private readonly ViewerContextResolver _viewers;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdateCommentCommandHandler(
        ICommentRepository comments,
        ViewerContextResolver viewers,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _comments = comments;
        _viewers = viewers;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CommentResponse>> Handle(
        UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CommentResponse>(CommunityErrors.NotAuthenticated);
        }

        var comment = await _comments.GetAsync(
            CommentId.From(request.CommentId), cancellationToken);

        if (comment is null)
        {
            return Result.Failure<CommentResponse>(CommunityErrors.CommentNotFound);
        }

        if (comment.AuthorId != userId)
        {
            return Result.Failure<CommentResponse>(CommunityErrors.CommentNotOwned);
        }

        var body = CommentBody.Create(request.Request.Body);

        if (body.IsFailure)
        {
            return Result.Failure<CommentResponse>(body.Error);
        }

        var edited = comment.Edit(body.Value, _clock.UtcNow);

        if (edited.IsFailure)
        {
            return Result.Failure<CommentResponse>(edited.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        return Result.Success(CommentMapper.ToResponse(
            CommentThread.Assemble(comment.Content, [comment]).Roots[0],
            viewer,
            new Dictionary<string, Integration.SocialSummary>(StringComparer.Ordinal)));
    }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Mentions;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Extensibility.Mentions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using MediaId = Diaspora.SharedKernel.Identity.MediaId;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>
/// Adds a comment.
/// <para>
/// Mentions are resolved here rather than in the service, because a comment mention has a lower
/// ceiling than a post mention and because only the handler has the request context. Everything
/// else â€” threading, the closed-thread rule, counter maintenance â€” is the shared service, so an
/// in-process caller and an HTTP caller get identical behaviour.
/// </para>
/// </summary>
public sealed class AddCommentCommandHandler : ICommandHandler<AddCommentCommand, CommentResponse>
{
    private readonly CommentService _commentService;
    private readonly ICommentRepository _comments;
    private readonly ContentReferenceResolver _references;
    private readonly MentionExtractor _mentions;
    private readonly IMentionRepository _mentionRepository;
    private readonly ViewerContextResolver _viewers;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly MediaAttachmentClaimer _media;
    private readonly CommunityAuthorNames _authorNames;

    public AddCommentCommandHandler(
        CommentService commentService,
        ICommentRepository comments,
        ContentReferenceResolver references,
        MentionExtractor mentions,
        IMentionRepository mentionRepository,
        ViewerContextResolver viewers,
        ICurrentUser currentUser,
        IClock clock,
        MediaAttachmentClaimer media,
        CommunityAuthorNames authorNames)
    {
        _commentService = commentService;
        _comments = comments;
        _references = references;
        _mentions = mentions;
        _mentionRepository = mentionRepository;
        _viewers = viewers;
        _currentUser = currentUser;
        _clock = clock;
        _media = media;
        _authorNames = authorNames;
    }

    public async Task<Result<CommentResponse>> Handle(
        AddCommentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CommentResponse>(CommunityErrors.NotAuthenticated);
        }

        var parentId = request.Request.ParentCommentId is { } parent
            ? CommentId.From(parent)
            : null;

        // A reply names its parent and nothing else â€” the reply route has no content in its
        // path, and asking a client to repeat what the parent already knows is how a reply ends
        // up on the wrong content.
        var content = request.ContentType ?? request.Request.ContentType;
        var contentId = request.ContentId ?? request.Request.ContentId;

        if (content is null && parentId is not null)
        {
            var parentComment = await _comments.GetAsync(parentId, cancellationToken);

            if (parentComment is null)
            {
                return Result.Failure<CommentResponse>(CommunityErrors.CommentNotFound);
            }

            content = CommunityWireNames.ToWire(parentComment.Content.Type);
            contentId = parentComment.Content.Id;
        }

        var reference = _references.Resolve(content, contentId ?? Guid.Empty);

        if (reference.IsFailure)
        {
            return Result.Failure<CommentResponse>(reference.Error);
        }

        var added = await _commentService.AddAsync(
            reference.Value, userId, request.Request.Body, parentId, cancellationToken);

        if (added.IsFailure)
        {
            return Result.Failure<CommentResponse>(added.Error);
        }

        var comment = added.Value;

        if (request.Request.QuotedCommentId is { } quoted)
        {
            comment.Quote(CommentId.From(quoted));
        }

        if (request.Request.AttachmentMediaId is { } mediaId)
        {
            comment.AttachMedia(MediaId.From(mediaId));

            // Claiming is what takes the asset out of Media's orphan population. Without it
            // the upload succeeds, the comment renders, and the sweeper collects the image a
            // day later — which is what happened to every comment attachment from B6 until
            // PSR-1, because Media's enum had no Comment owner type to claim it under.
            // Failures are logged and never fail the comment; see MediaAttachmentClaimer.
            await _media.ClaimAsync(comment, cancellationToken);
        }

        var resolved = await _mentions.ExtractAsync(
            comment.Body.Value, CommunityLimits.MaxMentionsPerComment, cancellationToken);

        foreach (var (candidate, target) in resolved)
        {
            comment.AddMention(target, candidate.StartOffset, candidate.Length);

            if (target.IsResolved)
            {
                await _mentionRepository.AddAsync(
                    Mention.Create(comment.Reference, userId, target, _clock.UtcNow),
                    cancellationToken);
            }
        }

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var names = await _authorNames.ResolveAsync([comment.AuthorId], cancellationToken);

        return Result.Success(CommentMapper.ToResponse(
            CommentThread.Assemble(reference.Value, [comment]).Roots[0],
            viewer,
            new Dictionary<string, Integration.SocialSummary>(StringComparer.Ordinal),
            names));
    }
}

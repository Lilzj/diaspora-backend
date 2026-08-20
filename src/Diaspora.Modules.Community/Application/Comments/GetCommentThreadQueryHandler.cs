using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>
/// Reads a thread.
/// <para>
/// Two queries, not two per root: a page of top-level comments, then every reply to that page in
/// one batch. A thread of two hundred comments is two round trips, and the shape does not change
/// as it grows.
/// </para>
/// </summary>
public sealed class GetCommentThreadQueryHandler
    : IQueryHandler<GetCommentThreadQuery, CommentThreadResponse>
{
    private readonly ICommentRepository _comments;
    private readonly ContentReferenceResolver _references;
    private readonly ICommunitySocialService _social;
    private readonly ViewerContextResolver _viewers;

    public GetCommentThreadQueryHandler(
        ICommentRepository comments,
        ContentReferenceResolver references,
        ICommunitySocialService social,
        ViewerContextResolver viewers)
    {
        _comments = comments;
        _references = references;
        _social = social;
        _viewers = viewers;
    }

    public async Task<Result<CommentThreadResponse>> Handle(
        GetCommentThreadQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<CommentThreadResponse>(reference.Error);
        }

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var roots = await _comments.ListRootsAsync(
            reference.Value,
            FeedCursor.Decode(request.Cursor),
            Math.Clamp(request.Limit, 1, CommunityLimits.MaxFeedPageSize),
            includeHidden: false,
            cancellationToken);

        var replies = roots.Items.Count == 0
            ? []
            : await _comments.ListRepliesAsync(
                roots.Items.Select(comment => comment.Id).ToList(),
                CommunityLimits.MaxFeedPageSize,
                cancellationToken);

        var all = new List<Comment>(roots.Items);
        all.AddRange(replies);

        var thread = CommentThread.Assemble(reference.Value, all);

        var social = await _social.GetSummariesAsync(
            all.Select(comment => comment.Reference).ToList(), viewer.UserId, cancellationToken);

        var descriptor = await _references.DescribeAsync(reference.Value, cancellationToken);

        return Result.Success(new CommentThreadResponse
        {
            ContentType = CommunityWireNames.ToWire(reference.Value.Type),
            ContentId = reference.Value.Id,
            Comments = thread.Roots
                .Select(node => CommentMapper.ToResponse(node, viewer, social))
                .ToList(),
            TotalCount = await _comments.CountAsync(reference.Value, cancellationToken),
            NextCursor = roots.NextCursor,
            HasMore = roots.HasMore,
            CommentsClosed = descriptor is not null && !descriptor.AcceptsComments
        });
    }
}

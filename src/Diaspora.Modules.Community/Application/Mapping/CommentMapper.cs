using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Integration;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>
/// Projects comment trees.
/// <para>
/// A tombstoned comment is returned with an empty body and the flag set, never omitted. Dropping
/// it would take its replies with it and leave a conversation with a hole in the middle.
/// </para>
/// </summary>
public static class CommentMapper
{
    /// <summary>What a removed or deleted comment says instead of its body.</summary>
    public const string TombstoneBody = "";

    public static CommentResponse ToResponse(
        CommentNode node,
        ViewerContext viewer,
        IReadOnlyDictionary<string, SocialSummary> social,
        IReadOnlyDictionary<Guid, string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(social);

        var comment = node.Comment;
        var summary = social.GetValueOrDefault(comment.Reference.Key);
        var isOwner = viewer.UserId is { } id && id == comment.AuthorId;

        return new CommentResponse
        {
            CommentId = comment.Id.Value,
            ContentType = CommunityWireNames.ToWire(comment.Content.Type),
            ContentId = comment.Content.Id,
            Author = AuthorMapper.ToSummary(comment, viewer, names),
            AuthorId = comment.AuthorId.Value,
            AuthorName = AuthorMapper.NameOf(comment.AuthorId.Value, names),
            AuthorReputationLevel = null,
            ParentCommentId = comment.ParentCommentId?.Value,
            QuotedCommentId = comment.QuotedCommentId?.Value,
            Body = comment.IsTombstoned ? TombstoneBody : comment.Body.Value,
            Mentions = comment.Mentions
                .Where(mention => mention.Target.IsResolved)
                .Select(mention => mention.Target.TargetId!.Value)
                .ToList(),
            AttachmentMediaId = comment.AttachmentMediaId?.Value,
            LikeCount = comment.LikeCount,
            IsLikedByUser = summary?.ViewerReactions.Contains(
                ReactionMapper.Like, StringComparer.Ordinal) ?? false,
            Reactions = ReactionMapper.ToResponse(summary),
            IsPinned = comment.IsPinned,
            IsEdited = comment.IsEdited,
            IsTombstoned = comment.IsTombstoned,
            Status = CommunityWireNames.ToWire(comment.Status),
            Depth = comment.Depth,
            RenderedDepth = node.RenderedDepth,
            Replies = node.Replies.Select(reply => ToResponse(reply, viewer, social, names)).ToList(),
            ReplyCount = comment.ReplyCount,
            CanEdit = isOwner && !comment.IsTombstoned,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.EditedAt
        };
    }
}

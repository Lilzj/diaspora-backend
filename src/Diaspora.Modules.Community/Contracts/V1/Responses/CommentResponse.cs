namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// A comment and its replies.
/// <para>
/// Recursive, as the client's own comment type is. A removed or deleted comment arrives with an
/// empty body and <c>isTombstoned</c> set rather than being omitted — the thread keeps its shape
/// and a reply does not lose the thing it was answering.
/// </para>
/// </summary>
public sealed record CommentResponse
{
    public required Guid CommentId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required AuthorSummaryResponse Author { get; init; }

    /// <summary>The client reads the author name from here as well as from the author block.</summary>
    public required Guid AuthorId { get; init; }

    public string? AuthorName { get; init; }

    public string? AuthorReputationLevel { get; init; }

    public Guid? ParentCommentId { get; init; }

    public Guid? QuotedCommentId { get; init; }

    public required string Body { get; init; }

    public required IReadOnlyList<Guid> Mentions { get; init; }

    public Guid? AttachmentMediaId { get; init; }

    public required int LikeCount { get; init; }

    public required bool IsLikedByUser { get; init; }

    public required ReactionSummaryResponse Reactions { get; init; }

    public required bool IsPinned { get; init; }

    public required bool IsEdited { get; init; }

    /// <summary>True when the body was withheld. The row is still here on purpose.</summary>
    public required bool IsTombstoned { get; init; }

    public required string Status { get; init; }

    public required int Depth { get; init; }

    /// <summary>The depth to draw at, which past the ceiling is less than <c>depth</c>.</summary>
    public required int RenderedDepth { get; init; }

    public required IReadOnlyList<CommentResponse> Replies { get; init; }

    public required int ReplyCount { get; init; }

    public required bool CanEdit { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

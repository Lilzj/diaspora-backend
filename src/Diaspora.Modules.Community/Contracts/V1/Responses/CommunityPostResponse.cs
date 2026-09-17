namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// A post, in full.
/// <para>
/// Field names are the union of B6's and the client's. <c>postId</c> is canonical and
/// <c>updateId</c> is served alongside it; <c>body</c> is canonical and <c>description</c> is
/// served alongside it. Neither client field is dropped, and neither B6 name is bent — the
/// frontend contract report records every one of these pairings.
/// </para>
/// </summary>
public sealed record CommunityPostResponse
{
    public required Guid PostId { get; init; }

    /// <summary>The client's own name for the same identifier.</summary>
    public required Guid UpdateId { get; init; }

    public required AuthorSummaryResponse Author { get; init; }

    public string? Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The client's own name for the body.</summary>
    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string Urgency { get; init; }

    public required string Status { get; init; }

    public required string ContentType { get; init; }

    public required string Visibility { get; init; }

    public required LocationContextResponse Location { get; init; }

    public required IReadOnlyList<PostAttachmentResponse> Attachments { get; init; }

    /// <summary>
    /// Media identifiers for the image attachments, in order.
    /// <para>
    /// The client's <c>imageUrls</c> in the only form this platform allows. Identifiers, not
    /// addresses — the name is kept close so the mapping is obvious, and the type is different so
    /// the rule is not quietly broken.
    /// </para>
    /// </summary>
    public required IReadOnlyList<Guid> ImageMediaIds { get; init; }

    public required IReadOnlyList<string> Hashtags { get; init; }

    public required IReadOnlyList<Guid> MentionedUserIds { get; init; }

    public PollResponse? Poll { get; init; }

    public required ReactionSummaryResponse Reactions { get; init; }

    public required int CommentCount { get; init; }

    public required int ShareCount { get; init; }

    public required int BookmarkCount { get; init; }

    public required int ViewCount { get; init; }

    public required bool BookmarkedByViewer { get; init; }

    public required string VerificationStatus { get; init; }

    public required int ConfirmationCount { get; init; }

    public required int VerifiedResidentConfirmationCount { get; init; }

    public required int RequiredConfirmations { get; init; }

    public required int RequiredVerifiedConfirmations { get; init; }

    /// <summary>Visible to the author and to staff only. Zero for everybody else.</summary>
    public required int ReportCount { get; init; }

    public required bool IsPinned { get; init; }

    public required bool IsEdited { get; init; }

    public required bool CommentsClosed { get; init; }

    public Guid? PinnedCommentId { get; init; }

    public required int RevisionCount { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required bool IsExpired { get; init; }

    public DateTimeOffset? EditedAt { get; init; }

    /// <summary>
    /// How far around its pin the alert applies, in metres. Null is an alert at a point — the map
    /// draws a circle only when this is set.
    /// </summary>
    public int? AffectedRadiusMetres { get; init; }

    /// <summary>When its author withdrew or cancelled it. Null while it is in effect.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// True when the caller may edit or delete this post. For a published alert this also means
    /// amending its urgency and affected area.
    /// </summary>
    public required bool CanEdit { get; init; }

    /// <summary>True when the caller may withdraw this post because the report was wrong.</summary>
    public required bool CanWithdraw { get; init; }

    /// <summary>True when the caller may cancel this post because what it reported is over.</summary>
    public required bool CanCancel { get; init; }
}

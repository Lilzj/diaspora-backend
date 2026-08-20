using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody commented on something.
/// <para>
/// Carries a content type and identifier rather than a post identifier, which is what lets one
/// notification handler serve comments on posts, guides, events and everything after them.
/// </para>
/// <para>
/// The content author and the parent author are separate fields, and collapsing them is the
/// commonest bug in a notification pipeline: a reply notifies the person replied to, a top-level
/// comment notifies the person who wrote the content, and one field cannot express both.
/// </para>
/// </summary>
public sealed record CommunityCommentAddedIntegrationEvent : IntegrationEvent
{
    public required Guid CommentId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid AuthorId { get; init; }

    public Guid? ParentCommentId { get; init; }

    /// <summary>Who wrote the thing being commented on. Null when Community does not own it.</summary>
    public Guid? ContentAuthorId { get; init; }

    /// <summary>Who wrote the comment being replied to. Null for a top-level comment.</summary>
    public Guid? ParentAuthorId { get; init; }

    public required int Depth { get; init; }
}

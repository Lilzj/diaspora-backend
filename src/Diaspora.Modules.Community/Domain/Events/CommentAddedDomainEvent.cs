using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a comment is added to anything.
/// <para>
/// Carries the content reference rather than a post identifier, which is what lets a
/// notification module handle a comment on a guide, an event and a post with one handler. It
/// also carries the parent author separately from the content author: a reply notifies the
/// person replied to, a top-level comment notifies the person who wrote the content, and
/// collapsing the two sends the wrong person a notification on every reply.
/// </para>
/// </summary>
public sealed record CommentAddedDomainEvent : DomainEvent
{
    public required CommentId CommentId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId AuthorId { get; init; }

    public CommentId? ParentCommentId { get; init; }

    /// <summary>The author of the content. Null when B6 does not own it.</summary>
    public Guid? ContentAuthorId { get; init; }

    /// <summary>The author of the parent comment, when this is a reply.</summary>
    public Guid? ParentAuthorId { get; init; }

    public required int Depth { get; init; }
}

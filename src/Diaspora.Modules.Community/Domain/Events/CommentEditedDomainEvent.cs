using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised when a comment body changes.</summary>
public sealed record CommentEditedDomainEvent : DomainEvent
{
    public required CommentId CommentId { get; init; }

    public required UserId EditedBy { get; init; }
}

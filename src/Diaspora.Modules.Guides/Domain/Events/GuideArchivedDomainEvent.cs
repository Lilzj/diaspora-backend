using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using UserId = Diaspora.SharedKernel.Identity.UserId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a published guide is retired. Consumers de-index; the versions stay, because
/// history is permanent even when the guide is not current.
/// </summary>
public sealed record GuideArchivedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>
    /// The person who archived it: the author, through the archive command. Null when
    /// moderation archived it, which names no person here.
    /// </summary>
    public UserId? ArchivedBy { get; init; }
}

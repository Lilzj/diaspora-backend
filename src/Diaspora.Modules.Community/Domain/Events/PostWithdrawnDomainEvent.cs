using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when an author retracts a post because the report was wrong. The post stays readable and
/// says so; a live map drops it.
/// </summary>
public sealed record PostWithdrawnDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UserId AuthorId { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }
}

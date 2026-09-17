using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when an author calls a post off because what it reported is over or will not happen.
/// </summary>
public sealed record PostCancelledDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UserId AuthorId { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }
}

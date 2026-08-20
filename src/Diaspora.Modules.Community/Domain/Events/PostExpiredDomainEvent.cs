using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a post passes its expiry.
/// <para>
/// Distinct from deletion on purpose. An expired post keeps its permalink and its comment
/// thread; it only leaves the feeds. A consumer that treats this as a delete throws away a
/// conversation that is still worth reading.
/// </para>
/// </summary>
public sealed record PostExpiredDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required DateTimeOffset ExpiredAt { get; init; }
}

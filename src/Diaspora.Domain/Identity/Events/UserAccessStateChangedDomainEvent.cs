using Diaspora.Domain.Identity.AccessStates;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised on every movement along the progressive access ladder. Downstream modules
/// key off this rather than off individual lifecycle events.
/// </summary>
public sealed record UserAccessStateChangedDomainEvent(
    UserId UserId,
    string FromState,
    string ToState,
    AccessStateChangeReason Reason,
    string? Notes,
    DateTimeOffset ChangedAt) : DomainEvent
{
    /// <summary>When a suspension ends, if it was given an end. Null otherwise.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// The reason code staff chose for a suspension or closure (7 Nov 2026). Null for every other move.
    /// </summary>
    public string? ReasonCode { get; init; }
}

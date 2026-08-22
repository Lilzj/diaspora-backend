using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised when a member's identity has been erased at their own request.
/// <para>
/// Distinct from <see cref="UserAccountDisabledDomainEvent"/>, which a member can come back from.
/// This one is terminal: the profile fields that named a person have been replaced and there is
/// nothing left to reactivate. Consumers that cache a display name or an avatar need this event to
/// know their copy is now a copy of somebody who asked not to be there.
/// </para>
/// </summary>
public sealed record UserAnonymizedDomainEvent(
    UserId UserId,
    DateTimeOffset AnonymizedAt) : DomainEvent;

using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// A member's platform role changed (7 Nov 2026). Raised so the member is told, with the reason
/// given; until then a role changed in silence.
/// </summary>
public sealed record UserRoleChangedDomainEvent(
    UserId UserId,
    string PreviousRole,
    string NewRole,
    string? Reason,
    DateTimeOffset ChangedAt) : DomainEvent;

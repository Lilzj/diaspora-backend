using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised on a security hard lock, not on a transient failed-login lockout.</summary>
public sealed record UserAccountLockedDomainEvent(
    UserId UserId,
    string Reason,
    DateTimeOffset LockedAt) : DomainEvent;

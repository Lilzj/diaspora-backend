using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised on a failed credential exchange against a known account.</summary>
public sealed record UserLoginFailedDomainEvent(
    UserId UserId,
    int FailedAttempts,
    bool LockedOut,
    DateTimeOffset AttemptedAt) : DomainEvent;

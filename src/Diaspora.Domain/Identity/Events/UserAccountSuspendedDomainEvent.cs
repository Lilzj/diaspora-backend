using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when moderation suspends an account.</summary>
public sealed record UserAccountSuspendedDomainEvent(
    UserId UserId,
    string Reason,
    DateTimeOffset? SuspendedUntil,
    DateTimeOffset SuspendedAt) : DomainEvent;

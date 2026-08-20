using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised for both a deliberate change and a recovery-driven reset.</summary>
public sealed record UserPasswordChangedDomainEvent(
    UserId UserId,
    bool WasReset,
    DateTimeOffset ChangedAt) : DomainEvent;

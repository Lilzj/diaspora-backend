using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when an account is closed.</summary>
public sealed record UserAccountDisabledDomainEvent(
    UserId UserId,
    string Reason,
    DateTimeOffset DisabledAt) : DomainEvent;

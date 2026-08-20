using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a member deliberately ends a session.</summary>
public sealed record UserLoggedOutDomainEvent(
    UserId UserId,
    SessionId SessionId,
    DateTimeOffset LoggedOutAt) : DomainEvent;

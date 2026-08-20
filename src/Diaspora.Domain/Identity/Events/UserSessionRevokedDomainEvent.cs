using Diaspora.Domain.Identity.Sessions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a session is terminated, whatever the cause.</summary>
public sealed record UserSessionRevokedDomainEvent(
    UserId UserId,
    SessionId SessionId,
    SessionRevocationReason Reason,
    DateTimeOffset RevokedAt) : DomainEvent;

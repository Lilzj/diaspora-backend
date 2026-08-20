using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a session mints a refresh token for the first time.</summary>
public sealed record RefreshTokenIssuedDomainEvent(
    UserId UserId,
    SessionId SessionId,
    RefreshTokenId TokenId,
    DateTimeOffset ExpiresAt) : DomainEvent;

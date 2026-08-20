using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised when an already-rotated or revoked refresh token is replayed, the classic
/// signature of a stolen token. The session is revoked before this is published.
/// </summary>
public sealed record RefreshTokenReuseDetectedDomainEvent(
    UserId UserId,
    SessionId SessionId,
    RefreshTokenId ReplayedTokenId,
    DateTimeOffset DetectedAt) : DomainEvent;

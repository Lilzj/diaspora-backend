using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised on every successful refresh-token rotation.</summary>
public sealed record RefreshTokenRotatedDomainEvent(
    UserId UserId,
    SessionId SessionId,
    RefreshTokenId PreviousTokenId,
    RefreshTokenId NewTokenId,
    DateTimeOffset RotatedAt) : DomainEvent;

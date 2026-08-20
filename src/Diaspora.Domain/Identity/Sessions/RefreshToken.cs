using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// One link in a session's refresh-token rotation chain.
/// <para>
/// Only the hash is persisted. The plaintext token exists for exactly as long as it
/// takes to put it in the HTTP response, so a database disclosure yields nothing an
/// attacker can redeem.
/// </para>
/// <para>
/// Rotated links are retained rather than deleted: replaying one is the signal that
/// a token was stolen, and detection depends on the record still being there.
/// </para>
/// </summary>
public sealed class RefreshToken : Entity<RefreshTokenId>
{
    public SessionId SessionId { get; private set; } = default!;
    public SecretHash TokenHash { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public RefreshTokenId? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    private RefreshToken(
        RefreshTokenId id,
        SessionId sessionId,
        SecretHash tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) : base(id)
    {
        SessionId = sessionId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    internal static RefreshToken Issue(
        SessionId sessionId,
        SecretHash tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new(RefreshTokenId.New(), sessionId, tokenHash, createdAt, expiresAt);

    public RefreshTokenStatus StatusAt(DateTimeOffset now)
    {
        if (RevokedAt is not null) return RefreshTokenStatus.Revoked;
        if (RotatedAt is not null) return RefreshTokenStatus.Rotated;
        return ExpiresAt <= now ? RefreshTokenStatus.Expired : RefreshTokenStatus.Active;
    }

    public bool IsRedeemableAt(DateTimeOffset now) => StatusAt(now) == RefreshTokenStatus.Active;

    /// <summary>
    /// True when the token was consumed or invalidated at some point — replaying it
    /// means someone is holding a credential they should no longer have.
    /// </summary>
    public bool WasConsumed => RotatedAt is not null || RevokedAt is not null;

    internal void MarkRotated(DateTimeOffset now, RefreshTokenId replacedBy)
    {
        RotatedAt = now;
        ReplacedByTokenId = replacedBy;
    }

    internal void MarkRevoked(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}

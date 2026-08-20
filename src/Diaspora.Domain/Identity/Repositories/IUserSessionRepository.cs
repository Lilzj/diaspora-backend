using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Persistence contract for the <see cref="UserSession"/> aggregate.
/// </summary>
public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads just the fields per-request token validation needs — no rotation chain, no
    /// change tracking. This is the hot path; the full <see cref="GetByIdAsync"/> is for the
    /// rotation path that actually mutates the session.
    /// </summary>
    Task<SessionValidationSnapshot?> GetValidationSnapshotAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the session that owns a presented refresh token, including the full
    /// rotation chain. Rotated and revoked links must be returned too — reuse detection
    /// depends on being able to see a token that is no longer redeemable.
    /// </summary>
    Task<UserSession?> GetByRefreshTokenHashAsync(
        SecretHash tokenHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSession>> ListByUserAsync(
        UserId userId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSession>> ListActiveByUserAsync(
        UserId userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByUserAsync(
        UserId userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    void Update(UserSession session);
}

using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="IUserSessionRepository" />
public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public UserSessionRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<UserSession> Sessions => _dbContext.Set<UserSession>();

    private DbSet<RefreshToken> RefreshTokens => _dbContext.Set<RefreshToken>();

    public async Task<UserSession?> GetByIdAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default) =>
        await Sessions
            .Include(session => session.RefreshTokens)
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

    public async Task<SessionValidationSnapshot?> GetValidationSnapshotAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default) =>
        await Sessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId)
            .Select(session => new SessionValidationSnapshot(
                session.Id,
                session.UserId,
                session.Status,
                session.AbsoluteExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSession?> GetByRefreshTokenHashAsync(
        SecretHash tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        // Resolved in two steps: find the token by its indexed hash, then load its session
        // with the full chain. Filtering sessions by a nested collection predicate would
        // produce a far worse plan on a table that grows with every refresh.
        var sessionId = await RefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => token.SessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sessionId is null)
        {
            return null;
        }

        // The whole rotation chain is loaded, spent links included. Reuse detection only
        // works because a retired token is still present to be recognised.
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSession>> ListByUserAsync(
        UserId userId,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = Sessions.Where(session => session.UserId == userId);

        if (activeOnly)
        {
            query = query.Where(session => session.Status == SessionStatus.Active);
        }

        return await query
            .OrderByDescending(session => session.LastSeenAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSession>> ListActiveByUserAsync(
        UserId userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        await Sessions
            .Include(session => session.RefreshTokens)
            .Where(session =>
                session.UserId == userId &&
                session.Status == SessionStatus.Active &&
                session.AbsoluteExpiresAt > asOf)
            .OrderByDescending(session => session.LastSeenAt)
            .ToListAsync(cancellationToken);

    public async Task<int> CountActiveByUserAsync(
        UserId userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        await Sessions.CountAsync(
            session =>
                session.UserId == userId &&
                session.Status == SessionStatus.Active &&
                session.AbsoluteExpiresAt > asOf,
            cancellationToken);

    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default) =>
        await Sessions.AddAsync(session, cancellationToken);

    public void Update(UserSession session) =>
        TrackedEntityUpdater.MarkModified(_dbContext, Sessions, session);
}

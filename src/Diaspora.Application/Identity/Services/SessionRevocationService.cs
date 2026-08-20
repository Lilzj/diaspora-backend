using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Services;

/// <inheritdoc cref="ISessionRevocationService" />
public sealed class SessionRevocationService : ISessionRevocationService
{
    private readonly IUserSessionRepository _sessions;
    private readonly ILogger<SessionRevocationService> _logger;

    public SessionRevocationService(
        IUserSessionRepository sessions,
        ILogger<SessionRevocationService> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public Task<int> RevokeAllAsync(
        UserId userId,
        SessionRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        RevokeAsync(userId, exceptSessionId: null, reason, now, cancellationToken);

    public Task<int> RevokeAllExceptAsync(
        UserId userId,
        SessionId exceptSessionId,
        SessionRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        RevokeAsync(userId, exceptSessionId, reason, now, cancellationToken);

    private async Task<int> RevokeAsync(
        UserId userId,
        SessionId? exceptSessionId,
        SessionRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var sessions = await _sessions.ListActiveByUserAsync(userId, now, cancellationToken);
        var revoked = 0;

        foreach (var session in sessions)
        {
            if (exceptSessionId is not null && session.Id == exceptSessionId)
            {
                continue;
            }

            // Already-revoked sessions come back as a failed Result; that is expected here
            // and is not worth surfacing, so the count reflects what actually changed.
            if (session.Revoke(reason, now).IsSuccess)
            {
                _sessions.Update(session);
                revoked++;
            }
        }

        if (revoked > 0)
        {
            _logger.LogInformation(
                "Revoked {Count} session(s) for user {UserId} with reason {Reason}",
                revoked, userId, reason);
        }

        return revoked;
    }
}

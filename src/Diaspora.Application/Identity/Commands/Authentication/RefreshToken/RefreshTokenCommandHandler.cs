using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Authentication.RefreshToken;

/// <summary>
/// Rotates a refresh token.
/// <para>
/// Every exchange invalidates the presented token and issues a new one. The retired links
/// are kept, not deleted, because a replay of one of them is the clearest available signal
/// that a token has been stolen: a well-behaved client never presents the same refresh
/// token twice. When that happens the aggregate tears the whole session down, which signs
/// out the attacker and the genuine member together — the right trade when the alternative
/// is leaving an intruder with a live session.
/// </para>
/// </summary>
public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private readonly IUserSessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenIssuer _refreshTokens;
    private readonly ITokenIssuanceService _tokenIssuance;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAvatarUrlResolver _avatars;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserSessionRepository sessions,
        IUserRepository users,
        IRefreshTokenIssuer refreshTokens,
        ITokenIssuanceService tokenIssuance,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IUnitOfWork unitOfWork,
        IAvatarUrlResolver avatars,
        IClock clock,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _sessions = sessions;
        _users = users;
        _refreshTokens = refreshTokens;
        _tokenIssuance = tokenIssuance;
        _securityEvents = securityEvents;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _avatars = avatars;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var presentedHash = _refreshTokens.ComputeHash(request.RefreshToken);

        var session = await _sessions.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);
        if (session is null)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                Type = SecurityEventType.LoginFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = "Refresh attempted with an unknown token."
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(SessionErrors.RefreshTokenNotFound);
        }

        var replacement = _refreshTokens.Issue(now);

        var rotation = session.RotateRefreshToken(
            presentedHash, replacement.Hash, now, _policies.Current.RefreshTokenLifetime);

        _sessions.Update(session);

        if (rotation.IsFailure)
        {
            if (rotation.Error == SessionErrors.RefreshTokenReuseDetected)
            {
                await HandleReuseAsync(session, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(rotation.Error);
        }

        var user = await _users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthenticationResponse>(UserErrors.NotFound);
        }

        // A session that predates a suspension or lock must not be able to outlive it by
        // quietly refreshing, so the account state is re-checked on every rotation.
        var canAuthenticate = user.EnsureCanAuthenticate(now);
        if (canAuthenticate.IsFailure)
        {
            session.Revoke(SessionRevocationReason.AccountStateChanged, now);
            _sessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<AuthenticationResponse>(canAuthenticate.Error);
        }

        user.RecordActivity(now);
        _users.Update(user);

        var effectiveRefreshToken = replacement with { ExpiresAt = rotation.Value.ExpiresAt };

        var issuance = _tokenIssuance.IssueForExistingSession(user, session, effectiveRefreshToken);
        if (issuance.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(issuance.Error);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = session.Id,
            Type = SecurityEventType.TokenRefreshed,
            Severity = SecurityEventSeverity.Informational,
            Description = "Refresh token rotated."
        }, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request spent this same token between our read and our write. The
            // aggregate's WasConsumed check cannot see that — it read a snapshot taken
            // before the other write landed — so the database is the only thing that can
            // arbitrate, and it just did: our UPDATE matched zero rows because
            // rotated_at is no longer null.
            //
            // Treated as a replay rather than a retryable conflict. A well-behaved client
            // presents a refresh token once; two simultaneous presentations mean either a
            // duplicated request or a stolen token, and neither deserves a second chance
            // at a live session. The response is identical to the reuse path so a caller
            // cannot tell which branch it hit.
            _logger.LogWarning(
                "Concurrent rotation of the same refresh token on session {SessionId}; " +
                "the losing request was refused.",
                session.Id);

            return Result.Failure<AuthenticationResponse>(SessionErrors.RefreshTokenReuseDetected);
        }

        return Result.Success(TokenMapper.ToAuthenticationResponse(issuance.Value, user, _avatars));
    }

    private async Task HandleReuseAsync(UserSession session, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Refresh token reuse detected on session {SessionId} for user {UserId}; session revoked.",
            session.Id, session.UserId);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = session.UserId,
            SessionId = session.Id,
            Type = SecurityEventType.RefreshTokenReuseDetected,
            Severity = SecurityEventSeverity.Critical,
            Description = "A previously rotated refresh token was replayed. The session was revoked.",
            Metadata = new Dictionary<string, string>
            {
                ["deviceName"] = session.DeviceName,
                ["platform"] = session.Platform.ToString()
            }
        }, cancellationToken);
    }
}

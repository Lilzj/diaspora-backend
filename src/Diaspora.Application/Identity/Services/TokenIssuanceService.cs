using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Services;

/// <inheritdoc cref="ITokenIssuanceService" />
public sealed class TokenIssuanceService : ITokenIssuanceService
{
    private readonly IUserSessionRepository _sessions;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IRefreshTokenIssuer _refreshTokens;
    private readonly IAccessGateway _accessGateway;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IRequestContext _requestContext;
    private readonly IClock _clock;

    public TokenIssuanceService(
        IUserSessionRepository sessions,
        IAccessTokenIssuer accessTokens,
        IRefreshTokenIssuer refreshTokens,
        IAccessGateway accessGateway,
        IIdentityPolicyProvider policies,
        IRequestContext requestContext,
        IClock clock)
    {
        _sessions = sessions;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
        _accessGateway = accessGateway;
        _policies = policies;
        _requestContext = requestContext;
        _clock = clock;
    }

    public async Task<Result<AuthenticationOutcome>> IssueForNewSessionAsync(
        User user,
        SessionCreationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(context);

        var policies = _policies.Current;
        var now = _clock.UtcNow;

        var refreshToken = _refreshTokens.Issue(now);

        var session = UserSession.Start(
            user.Id,
            context.DeviceId,
            context.DeviceName,
            context.Platform,
            context.AppVersion,
            context.IpAddress,
            context.UserAgent,
            refreshToken.Hash,
            now,
            policies.RefreshTokenLifetime,
            policies.AbsoluteSessionLifetime);

        await _sessions.AddAsync(session, cancellationToken);

        // The session may cap the refresh token's life below the configured lifetime when
        // the absolute session ceiling is nearer. Report what was actually stored.
        var storedToken = session.CurrentRefreshToken(now);
        var effectiveRefreshToken = storedToken is null
            ? refreshToken
            : refreshToken with { ExpiresAt = storedToken.ExpiresAt };

        return Result.Success(Compose(user, session, effectiveRefreshToken, now));
    }

    public Result<AuthenticationOutcome> IssueForExistingSession(
        User user,
        UserSession session,
        RefreshTokenDescriptor rotatedRefreshToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rotatedRefreshToken);

        return Result.Success(Compose(user, session, rotatedRefreshToken, _clock.UtcNow));
    }

    private AuthenticationOutcome Compose(
        User user,
        UserSession session,
        RefreshTokenDescriptor refreshToken,
        DateTimeOffset now)
    {
        var decision = _accessGateway.Resolve(user, now);

        var accessToken = _accessTokens.Issue(new AccessTokenRequest
        {
            UserId = user.Id,
            SessionId = session.Id,
            DeviceId = session.DeviceId,
            Role = user.Role,
            AccessState = user.AccessState,
            SecurityStamp = user.Security.SecurityStamp,
            Permissions = decision.Permissions,
            Features = _policies.Current.IncludeFeaturesInAccessToken
                ? decision.Features.Granted
                : null,
            CorrelationId = _requestContext.CorrelationId
        });

        return new AuthenticationOutcome
        {
            Session = session,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Access = decision
        };
    }
}

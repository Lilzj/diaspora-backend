using System.Security.Claims;
using Diaspora.Domain.Identity.Repositories;
using Microsoft.IdentityModel.JsonWebTokens;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Api.Identity.Authentication;

/// <summary>
/// Post-signature validation of an access token against live state.
/// <para>
/// A valid signature only proves the token was issued by us and has not expired. It says
/// nothing about whether the session still exists, whether the member was suspended a
/// minute ago, or whether they signed out everywhere from another device. Without this
/// check, "revoke session" would mean "revoke in up to fifteen minutes", which is not
/// what anyone clicking that button believes they are doing.
/// </para>
/// <para>
/// Debug logging here is guarded explicitly rather than relying on the logger to
/// discard it: this runs on every authenticated request, and the argument expressions
/// would otherwise be evaluated even when debug logging is off.
/// </para>
/// <para>
/// The cost is one indexed lookup per authenticated request. That is the price of
/// revocation actually being immediate, and it is the reason the refresh token — not the
/// access token — carries the long lifetime.
/// </para>
/// </summary>
public static class AccessTokenValidationHandler
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AccessTokenValidationHandler));

        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("The token carried no principal.");
            return;
        }

        // A trusted service caller has no session and no account, so the member checks
        // below do not apply. Its authority comes from the permission claims the signature
        // covers; the endpoints it can reach are gated on permissions no human role holds.
        if (IsServiceToken(principal))
        {
            if (principal.FindFirst(DiasporaClaimTypes.SessionId) is not null)
            {
                // A token claiming to be both is malformed, and treating it as either
                // would be a guess. Refuse it.
                logger.LogWarning("Rejected a service token that also carried a session claim.");
                context.Fail("A service token must not carry a session claim.");
            }

            return;
        }

        // The subject is read as "sub", not ClaimTypes.NameIdentifier. Inbound claim
        // mapping is switched off so tokens stay compact, which means the registered JWT
        // names survive verbatim into the principal.
        if (!TryReadGuid(principal, JwtRegisteredClaimNames.Sub, out var userGuid) ||
            !TryReadGuid(principal, DiasporaClaimTypes.SessionId, out var sessionGuid))
        {
            context.Fail("The token is missing its subject or session claim.");
            return;
        }

        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var sessions = services.GetRequiredService<IUserSessionRepository>();

        // A projection, not the aggregate. This runs on every authenticated request, and
        // the full session drags in its entire refresh-token rotation chain — a cost that
        // grows with the session's age. The validator needs three fields; it reads three.
        var session = await sessions.GetValidationSnapshotAsync(SessionId.From(sessionGuid));

        if (session is null || !session.IsActiveAt(now))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Rejected an access token for session {SessionId}: the session is no longer active.",
                    sessionGuid);
            }

            context.Fail("The session is no longer active.");
            return;
        }

        if (session.UserId.Value != userGuid)
        {
            // The token's subject and session disagree. Either a forgery or a serious bug;
            // neither should be served.
            logger.LogWarning(
                "Access token subject {UserId} does not own session {SessionId}.", userGuid, sessionGuid);

            context.Fail("The token subject does not own the session.");
            return;
        }

        var users = services.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(UserId.From(userGuid));

        if (user is null)
        {
            context.Fail("The account no longer exists.");
            return;
        }

        // The kill switch. A password reset, hard lock or account closure bumps the stamp,
        // and every token minted before that moment stops validating here — no waiting for
        // expiry, no denylist to maintain.
        var stampClaim = principal.FindFirstValue(DiasporaClaimTypes.SecurityStamp);
        if (!int.TryParse(stampClaim, out var tokenStamp) || tokenStamp != user.Security.SecurityStamp)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Rejected a stale access token for user {UserId}: security stamp {TokenStamp} != {CurrentStamp}.",
                    userGuid, stampClaim, user.Security.SecurityStamp);
            }

            context.Fail("The token was invalidated by a security change on the account.");
            return;
        }

        var canAuthenticate = user.EnsureCanAuthenticate(now);
        if (canAuthenticate.IsFailure)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Rejected an access token for user {UserId}: {Error}.", userGuid, canAuthenticate.Error.Code);
            }

            context.Fail(canAuthenticate.Error.Description);
            return;
        }

        // No "last seen" heartbeat here. B2 called session.Touch(now) on the tracked
        // aggregate and relied on it never being saved — a side effect waiting for the
        // first SaveChanges later in the request to flush it, which for an expired session
        // would have silently written a revocation. The projection is untracked, so the
        // temptation is gone; the heartbeat belongs on an explicit, throttled write, not on
        // the authentication path.
    }

    private static bool IsServiceToken(ClaimsPrincipal principal) =>
        string.Equals(
            principal.FindFirstValue(DiasporaClaimTypes.TokenType),
            DiasporaClaimTypes.ServiceTokenType,
            StringComparison.Ordinal);

    private static bool TryReadGuid(ClaimsPrincipal principal, string claimType, out Guid value) =>
        Guid.TryParse(principal.FindFirstValue(claimType), out value);
}

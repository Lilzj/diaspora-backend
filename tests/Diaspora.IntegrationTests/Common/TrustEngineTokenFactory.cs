using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Models;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.UnitTests.Common;
using Microsoft.Extensions.Options;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Mints the service token the Trust &amp; Verification Engine would present.
/// <para>
/// Deliberately built from the same issuer the platform uses, with the same signing key,
/// so the tests exercise the real validation path rather than a bypass.
/// </para>
/// </summary>
public static class TrustEngineTokenFactory
{
    public const string ServiceName = "trust-engine";

    public static string Create(Guid subjectUserId)
    {
        var issuer = new ServiceTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "https://identity.diaspora.test",
                Audience = "https://api.diaspora.test",
                SigningKey = CustomWebApplicationFactory.TestSigningKey
            }),
            new FakeClock(DateTimeOffset.UtcNow));

        return issuer.Issue(new ServiceTokenRequest
        {
            ServiceName = ServiceName,
            Permissions = [IdentityPermissions.ApplyTrustDecision],
            CorrelationId = subjectUserId.ToString()
        }).Token;
    }

    /// <summary>
    /// A member access token carrying one extra permission.
    /// <para>
    /// <b>Minted rather than granted, and DEBT-031 is why.</b> This suite has no staff role to
    /// assign, so the reviewer paths — creator verification decisions, grant decisions, and the
    /// separation of applicant from reviewer that §29 and §30 of the Wave 6 brief require — would
    /// otherwise be untestable. The token goes through the platform's real validation path: same
    /// issuer, same audience, same signing key, same claim names. What it skips is the granting
    /// mechanism, which is Identity's and is covered by Identity's own suite.
    /// </para>
    /// <para>
    /// <b>It carries a real user identifier</b>, which a service token does not — a reviewer is a
    /// person, and the check that refuses somebody deciding their own application compares that
    /// identifier against the applicant's.
    /// </para>
    /// </summary>
    /// <para>
    /// <b>For in-process dispatch, not for HTTP.</b> The session identifier on it belongs to no
    /// session, and every authenticated request is checked against live session state — so
    /// presenting this at the API produces a 401 at the door, before any gate it was minted to
    /// exercise. Over HTTP, sign a real member in and change what they hold.
    /// </para>
    public static string CreateWithPermissions(Guid userId, params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var issuer = new JwtAccessTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "https://identity.diaspora.test",
                Audience = "https://api.diaspora.test",
                SigningKey = CustomWebApplicationFactory.TestSigningKey,
                AccessTokenMinutes = 15
            }),
            new FakeClock(DateTimeOffset.UtcNow));

        return issuer.Issue(new AccessTokenRequest
        {
            UserId = Diaspora.SharedKernel.Identity.UserId.From(userId),
            SessionId = Diaspora.SharedKernel.Identity.SessionId.New(),
            Role = Diaspora.Domain.Identity.Users.UserRole.CommunityUser,
            AccessState = Diaspora.Domain.Identity.AccessStates.AccessState.Verified,
            SecurityStamp = 1,
            Permissions = permissions
        }).Token;
    }

    /// <summary>
    /// A service token holding no permissions, for asserting that the trust endpoint is
    /// gated on the permission rather than merely on being a service.
    /// </summary>
    public static string CreateWithoutPermissions()
    {
        var issuer = new ServiceTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "https://identity.diaspora.test",
                Audience = "https://api.diaspora.test",
                SigningKey = CustomWebApplicationFactory.TestSigningKey
            }),
            new FakeClock(DateTimeOffset.UtcNow));

        return issuer.Issue(new ServiceTokenRequest
        {
            ServiceName = "unprivileged-service",
            Permissions = []
        }).Token;
    }
}

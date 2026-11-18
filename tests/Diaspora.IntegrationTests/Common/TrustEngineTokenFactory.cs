using System.Security.Claims;
using System.Text;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Models;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.UnitTests.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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
        return Mint(
            ServiceName,
            [IdentityPermissions.ApplyTrustDecision],
            correlationId: subjectUserId.ToString());
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
        return Mint("unprivileged-service", []);
    }

    /// <summary>
    /// Signs the token an outside service would present.
    /// <para>
    /// <b>Minted here rather than by the platform's issuer, and that is a correction rather than a
    /// shortcut.</b> This used to construct <c>ServiceTokenIssuer</c>, justified as using "the same
    /// issuer the platform uses" so the tests would exercise the real path. The premise was wrong:
    /// the platform never used it. BSEC-013 found that the issuer had no production caller at all,
    /// because the Trust &amp; Verification Engine runs outside this process and signs its own
    /// token with the shared key, by an implementation this repository does not contain. So the
    /// issuer was never the reference for what actually arrives — minting here models the real
    /// arrangement, where somebody else signs and this platform only validates.
    /// </para>
    /// <para>
    /// <b>Nothing about the validation path changes.</b> What these tests exercise is
    /// <c>AccessTokenValidationHandler</c>'s service-token branch, which is production code and
    /// untouched; a token that was shaped wrongly would be refused by it, which is the assertion.
    /// The claims below are the ones that branch reads: the token-type claim it switches on, the
    /// permission claims the endpoint is gated by, and deliberately no session claim — a service
    /// token carrying one is malformed and the handler refuses it.
    /// </para>
    /// </summary>
    private static string Mint(
        string serviceName, IReadOnlyCollection<string> permissions, string? correlationId = null)
    {
        var now = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, serviceName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(DiasporaClaimTypes.TokenType, DiasporaClaimTypes.ServiceTokenType)
        };

        claims.AddRange(permissions.Select(
            permission => new Claim(DiasporaClaimTypes.Permission, permission)));

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            claims.Add(new Claim(DiasporaClaimTypes.CorrelationId, correlationId));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://identity.diaspora.test",
            Audience = "https://api.diaspora.test",
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(5).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestSigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }
            .CreateToken(descriptor);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Puts a real caller behind <c>ICurrentUser</c> inside a plain DI scope.
/// <para>
/// Needed because a handler dispatched through MediatR outside a request has no
/// <c>HttpContext</c>, so <c>HttpContextCurrentUser</c> reports every caller as anonymous and
/// every <c>[Authorize]</c> attribute refuses. That is correct behaviour and not something to
/// work around by weakening the pipeline — so this seeds the accessor the pipeline already
/// reads, with the claims from the access token the test actually obtained.
/// </para>
/// <para>
/// The token is parsed, not validated: the API validated it when it was issued, and a test
/// asserting on authorisation wants the claims the caller holds rather than a second opinion
/// about their signature.
/// </para>
/// </summary>
internal static class ScopedCurrentUser
{
    public static void Apply(IServiceProvider services, string accessToken) =>
        Apply(services, accessToken, []);

    /// <summary>
    /// The same caller, holding extra permissions.
    /// <para>
    /// <b>For the privileged paths this suite has no role to grant.</b> DEBT-031 records that the
    /// integration host cannot mint a staff account, so a reviewer's decision — a creator
    /// verification, a grant approval — would otherwise be untestable, and §29 and §30 of the Wave 6
    /// brief require both to be proved.
    /// </para>
    /// <para>
    /// <b>What this skips is the granting, not the checking.</b> The command still runs through
    /// <c>AuthorizationBehavior</c>, which reads exactly these claims; a command whose permission is
    /// absent is still refused. How Identity decides to put a permission in a token is Identity's
    /// concern and its own suite's.
    /// </para>
    /// </summary>
    public static void Apply(
        IServiceProvider services, string accessToken, params string[] extraPermissions)
    {
        ArgumentNullException.ThrowIfNull(extraPermissions);

        var accessor = services.GetRequiredService<IHttpContextAccessor>();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        var claims = token.Claims.ToList();

        claims.AddRange(extraPermissions.Select(permission =>
            new Claim(Diaspora.Contracts.Authorization.PlatformClaimTypes.Permission, permission)));

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services
        };
    }
}

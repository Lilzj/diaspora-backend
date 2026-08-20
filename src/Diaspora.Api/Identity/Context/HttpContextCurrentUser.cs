using System.Security.Claims;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Diaspora.Api.Identity.Context;

/// <summary>
/// Backs the platform kernel's <c>ICurrentUser</c> with the presented access token.
/// <para>
/// Replaces B1's <c>DefaultCurrentUser</c>, which reported every caller as anonymous —
/// correct while no authentication existed, and silently wrong once it does. The
/// <c>AuthorizationBehavior</c> in the MediatR pipeline consults this, so leaving the
/// placeholder in place would have made every <c>[Authorize]</c> attribute inert.
/// </para>
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public UserId? UserId
    {
        get
        {
            // "sub", not ClaimTypes.NameIdentifier — inbound claim mapping is disabled.
            var value = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(value, out var parsed) ? SharedKernel.Identity.UserId.From(parsed) : null;
        }
    }

    public string? Role =>
        Principal?.FindFirstValue(DiasporaClaimTypes.Role) ?? Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Permissions =>
        Principal?.FindAll(DiasporaClaimTypes.Permission).Select(claim => claim.Value).ToArray() ?? [];

    public IReadOnlyDictionary<string, string> Claims =>
        Principal?.Claims
            // Multi-valued claims (permissions, features) would throw on a naive
            // ToDictionary. The first value wins here; callers that need the full set
            // use the typed properties above.
            .GroupBy(claim => claim.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}

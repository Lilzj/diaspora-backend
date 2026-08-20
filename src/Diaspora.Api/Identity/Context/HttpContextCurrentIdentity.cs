using System.Security.Claims;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Diaspora.Api.Identity.Context;

/// <summary>
/// Reads the identity-specific claims off the presented access token.
/// <para>
/// Lives in the API project because it is the only layer that knows about HTTP. The
/// Application layer sees only <see cref="ICurrentIdentityContext"/>, so a handler that
/// needs the caller's session id never has to reach for an <c>HttpContext</c>.
/// </para>
/// </summary>
public sealed class HttpContextCurrentIdentity : ICurrentIdentityContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentIdentity(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    // "sub", not ClaimTypes.NameIdentifier: inbound claim mapping is disabled so the
    // registered JWT names reach the principal unchanged.
    public UserId? UserId => ReadGuid(JwtRegisteredClaimNames.Sub) is { } id ? UserId.From(id) : null;

    public SessionId? SessionId =>
        ReadGuid(DiasporaClaimTypes.SessionId) is { } id ? SessionId.From(id) : null;

    public DeviceId? DeviceId =>
        ReadGuid(DiasporaClaimTypes.DeviceId) is { } id ? DeviceId.From(id) : null;

    public string? AccessState => Principal?.FindFirstValue(DiasporaClaimTypes.AccessState);

    private Guid? ReadGuid(string claimType)
    {
        var value = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

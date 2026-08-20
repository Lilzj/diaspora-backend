using System.Security.Claims;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.Platform.Authorization;
using Diaspora.SharedKernel.Primitives;
using Microsoft.AspNetCore.Http;

namespace Diaspora.Api.Identity.Context;

/// <summary>
/// Builds the platform's <see cref="AuthorizationSubject"/> from the presented access token.
/// <para>
/// The single place where Identity's vocabulary becomes the platform's. Replaces the
/// kernel's deny-everything default; every module's tier gate resolves through this without
/// referencing an access state or a user aggregate.
/// </para>
/// <para>
/// Reads the token, not the database. The token is re-validated against live session and
/// account state on every request by <c>AccessTokenValidationHandler</c>, so by the time an
/// authorisation handler runs, the claims have already been checked against reality — and
/// re-reading the account here would put a second query on every authorised request.
/// </para>
/// </summary>
public sealed class ClaimsAuthorizationSubjectAccessor : IAuthorizationSubjectAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsAuthorizationSubjectAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuthorizationSubject Current
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return AuthorizationSubject.Anonymous;
            }

            var role = ReadRole(principal);
            var accessState = ReadAccessState(principal);

            // A service token carries permissions and no member state. It is not a subject
            // any tier gate should satisfy, so it stays anonymous here — its authority comes
            // entirely from the permission claims the signature covers.
            if (accessState is null)
            {
                return AuthorizationSubject.Anonymous;
            }

            return AccessTierMap.ToSubject(accessState, role ?? UserRole.Guest);
        }
    }

    private static AccessState? ReadAccessState(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(DiasporaClaimTypes.AccessState);

        return !string.IsNullOrWhiteSpace(name) &&
               Enumeration.TryFromName<AccessState>(name, out var state)
            ? state
            : null;
    }

    private static UserRole? ReadRole(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(DiasporaClaimTypes.Role)
                   ?? principal.FindFirstValue(ClaimTypes.Role);

        return !string.IsNullOrWhiteSpace(name) &&
               Enumeration.TryFromName<UserRole>(name, out var role)
            ? role
            : null;
    }
}

using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Authorises every CQRS request, and <b>refuses one that declares nothing</b> (BSEC-003,
/// BSOLID-001).
/// <para>
/// <b>It used to return <c>next()</c> when it found no <see cref="AuthorizeAttribute"/>, and that
/// was the mechanism behind a HIGH security finding.</b> The host sets
/// <c>FallbackPolicy = null</c> deliberately, so a route with no <c>RequireAuthorization</c> whose
/// request carried no attribute was served to anonymous callers — and the architecture test a
/// comment in <c>IdentityAuthenticationRegistration</c> claimed would catch that had never been
/// written. One route was already reachable that way
/// (<c>GET /api/v1/creators/me/activity</c>), found by the audit rather than by the platform.
/// </para>
/// <para>
/// <b>Why the flip waited for a list to empty.</b> Making the absence of an attribute a refusal is
/// a dozen lines; doing it first would have refused live traffic. This behaviour runs on *every*
/// request, so the population was 247 request types — public reference content, provider webhooks,
/// commands dispatched from inside other handlers — not the 63 unannotated endpoints the audit's
/// remediation order assumed. Detection landed first
/// (<c>RequestsAwaitingAuthorizationReview.txt</c>), the 247 were classified one at a time, and
/// the refusal below is what the empty list bought.
/// </para>
/// <para>
/// <b>What an unmarked request now gets, and why it is a 401 rather than a 500.</b> A request type
/// nobody has classified is a request nobody has decided about, and the safe reading of an
/// undecided gate is "no". It surfaces as <c>Unauthorized</c> because that is what it would have
/// been had the author written the attribute they meant to: a caller sees the same refusal either
/// way, and the platform's own build is what tells the author — the architecture rules reject an
/// unannotated request type, so this path should be unreachable in a build that passes.
/// </para>
/// <para>
/// <b>It authorises; it does not scope.</b> "May this caller do this kind of thing" is answered
/// here. "May they do it to <em>this</em> row" is the handler's, and every handler that needs it
/// still asks — <c>RequireUser()</c> and the aggregate's own checks remain necessary.
/// </para>
/// <para>
/// <b>What BSOLID-001 still wants, recorded here rather than only in a commit message.</b> That
/// finding is the same defect stated as a shape, and its first criterion — an unannotated request
/// is refused, with a test proving it — is met above. Two are not. The attribute still carries
/// three independent questions on three nullable fields, so <c>[Authorize]</c> with nothing set
/// remains a valid state that a reader cannot distinguish from a half-finished thought; the
/// remediation asks for <c>[RequirePermission(x)]</c>, <c>[RequirePolicy(x)]</c> and
/// <c>[RequireRole(x)]</c> with required constructor arguments, so that an empty one does not
/// compile. And <c>Roles</c> is still a comma-separated string split at runtime, where a typo is a
/// silently never-authorised path rather than a compile error. Both are ergonomics rather than
/// exposure now that the absence of an attribute is refused, which is why they are a separate
/// change and not a reason to delay this one. BSOLID-001 stays OPEN in the ledger until they land.
/// </para>
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUser currentUser,
        IPermissionService permissionService,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttribute = typeof(TRequest).GetCustomAttribute<AuthorizeAttribute>();

        if (authorizeAttribute is null)
        {
            // Deliberately public, and it had to say so. The attribute carries the reason, which
            // is the whole difference between this and the absent attribute below.
            if (typeof(TRequest).GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            {
                return await next();
            }

            // Classified by nobody. Before BSEC-003 this line was `return await next()` and 247
            // request types took it.
            _logger.LogError(
                "{RequestName} declares neither [Authorize] nor [AllowAnonymous] and was refused. "
                + "This is a gap in the platform rather than anything the caller did: the "
                + "architecture rules reject an unannotated request type, so a passing build "
                + "should make this unreachable.",
                typeof(TRequest).Name);

            throw new Exceptions.UnauthorizedException("Authentication is required.");
        }

        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized access attempt to {RequestName}", typeof(TRequest).Name);
            throw new Exceptions.UnauthorizedException("Authentication is required.");
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Roles))
        {
            var requiredRoles = authorizeAttribute.Roles.Split(',', StringSplitOptions.TrimEntries);
            if (!requiredRoles.Contains(_currentUser.Role))
            {
                _logger.LogWarning(
                    "Forbidden access by user {UserId} with role {Role} to {RequestName}. Required: {RequiredRoles}",
                    _currentUser.UserId, _currentUser.Role, typeof(TRequest).Name, authorizeAttribute.Roles);
                throw new Exceptions.ForbiddenException("Insufficient role permissions.");
            }
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Permission))
        {
            var hasPermission = await _permissionService.HasPermissionAsync(authorizeAttribute.Permission, cancellationToken);
            if (!hasPermission)
            {
                throw new Exceptions.ForbiddenException($"Missing required permission: {authorizeAttribute.Permission}.");
            }
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Policy))
        {
            var authorized = await _permissionService.AuthorizeAsync(authorizeAttribute.Policy, cancellationToken);
            if (!authorized)
            {
                throw new Exceptions.ForbiddenException($"Policy '{authorizeAttribute.Policy}' not satisfied.");
            }
        }

        return await next();
    }
}

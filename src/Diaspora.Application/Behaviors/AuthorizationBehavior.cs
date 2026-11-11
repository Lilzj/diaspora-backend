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
/// <b>BSOLID-001 closed with it, on 10 November 2026, and this paragraph used to say it had
/// not.</b> That finding is the same defect stated as a shape: one attribute carrying three
/// independently nullable questions, so <c>[Authorize]</c> with nothing set was a valid state a
/// reader could not tell from a half-finished thought. It is gone. The vocabulary is
/// <c>[RequirePermission]</c>, <c>[RequirePolicy]</c>, <c>[RequireAuthentication]</c> and
/// <c>[AllowAnonymous]</c>, each with a required constructor argument, so an empty one does not
/// compile — and the old attribute was deleted rather than deprecated, which is what let the
/// compiler prove all 863 of its uses had been migrated. <c>Roles</c> went with it: the field had
/// zero users, every <c>Roles =</c> in the repository being <c>PermissionDefinition.Roles</c>, so
/// the finding's "type the role" item had nothing to type. DSP4005 reports an undeclared request
/// at the keystroke; this behaviour refuses one at runtime. Belt and brace.
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
        var requestType = typeof(TRequest);

        var permission = requestType.GetCustomAttribute<RequirePermissionAttribute>();
        var policy = requestType.GetCustomAttribute<RequirePolicyAttribute>();
        var authentication = requestType.GetCustomAttribute<RequireAuthenticationAttribute>();

        if (permission is null && policy is null && authentication is null)
        {
            // Deliberately public, and it had to say so. The attribute carries the reason, which
            // is the whole difference between this and the absent attribute below.
            if (requestType.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            {
                return await next();
            }

            // Classified by nobody. Before BSEC-003 this line was `return await next()` and 247
            // request types took it.
            _logger.LogError(
                "{RequestName} declares no authorization requirement and no [AllowAnonymous], and "
                + "was refused. This is a gap in the platform rather than anything the caller did: "
                + "the architecture rules and DSP4005 both reject an unannotated request type, so "
                + "a passing build should make this unreachable.",
                requestType.Name);

            throw new Exceptions.UnauthorizedException("Authentication is required.");
        }

        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized access attempt to {RequestName}", requestType.Name);
            throw new Exceptions.UnauthorizedException("Authentication is required.");
        }

        // Each requirement is checked when present, and the attributes are not exclusive: a
        // request may name a permission and a policy, and both must hold. What is gone is the
        // state where NONE was named and the behaviour passed anyway.
        if (permission is not null
            && !await _permissionService.HasPermissionAsync(permission.Permission, cancellationToken))
        {
            throw new Exceptions.ForbiddenException(
                $"Missing required permission: {permission.Permission}.");
        }

        if (policy is not null
            && !await _permissionService.AuthorizeAsync(policy.Policy, cancellationToken))
        {
            throw new Exceptions.ForbiddenException($"Policy '{policy.Policy}' not satisfied.");
        }

        // [RequireAuthentication] asks for nothing beyond the check above, which is the point of
        // it: "any signed-in member" is now a statement rather than an empty attribute.

        return await next();
    }
}

using Diaspora.Contracts.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Diaspora.Platform.Authorization.AspNetCore;

/// <summary>
/// Satisfies <see cref="PermissionRequirement"/>.
/// <para>
/// Reads the token's permission snapshot first, because that is what the issuer wrote and
/// it costs nothing. Falls back to resolving the catalogue against the caller's subject —
/// which covers tokens issued before a permission existed, and callers authenticated by
/// something other than a Diaspora access token.
/// </para>
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionCatalog _permissions;
    private readonly IAuthorizationSubjectAccessor _subjects;

    public PermissionAuthorizationHandler(
        IPermissionCatalog permissions,
        IAuthorizationSubjectAccessor subjects)
    {
        _permissions = permissions;
        _subjects = subjects;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var fromToken = context.User
            .FindAll(PlatformClaimTypes.Permission)
            .Any(claim => string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (fromToken || _permissions.IsGranted(requirement.Permission, _subjects.Current))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Satisfies <see cref="AccessTierRequirement"/> from the caller's subject.
/// <para>
/// Compares tiers rather than enumerating states. B2's handler special-cased "Verified"
/// and silently degraded every other requirement to "onboarded", so a policy added later
/// would have been quietly wrong. An ordered tier has no such failure mode.
/// </para>
/// </summary>
public sealed class AccessTierAuthorizationHandler : AuthorizationHandler<AccessTierRequirement>
{
    private readonly IAuthorizationSubjectAccessor _subjects;

    public AccessTierAuthorizationHandler(IAuthorizationSubjectAccessor subjects)
    {
        _subjects = subjects;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessTierRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var subject = _subjects.Current;

        // A restricted account never satisfies a tier gate. Its token may still assert the
        // tier it held when the enforcement action landed.
        if (subject.IsRestricted || subject.Tier == AccessTier.Anonymous)
        {
            return Task.CompletedTask;
        }

        var satisfied = requirement.RequiresExactTier
            ? subject.Tier == requirement.MinimumTier
            : subject.Tier >= requirement.MinimumTier;

        if (satisfied)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

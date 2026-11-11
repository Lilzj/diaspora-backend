using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Infrastructure.Identity.Authorization;

/// <summary>
/// Answers the platform kernel's permission contract from the token's permission snapshot.
/// <para>
/// Replaces the B1 placeholder that granted everything. Permissions come from the claims
/// the access token carries, so an authorisation check costs nothing; when the token is
/// silent the member's stored role and access state are consulted instead.
/// </para>
/// <para>
/// <b>Policies are a vocabulary over permissions and tiers, resolved through
/// <see cref="IPolicyCatalog"/>.</b> This class once said they "are expressed as permissions
/// rather than as a separate concept" and implemented that by passing a policy name to a
/// permission lookup — which could only ever answer false, because the two naming conventions are
/// deliberately disjoint. The catalogue is where the mapping actually lives, and it is the same
/// one the host registers with ASP.NET Core, so an endpoint and the request behind it now reach
/// the same verdict from the same name.
/// </para>
/// </summary>
public sealed class ClaimsPermissionService : IPermissionService
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IUserRepository _users;
    private readonly IPolicyCatalog _policies;
    private readonly IAuthorizationSubjectAccessor _subjects;

    public ClaimsPermissionService(
        ICurrentUser currentUser,
        ICurrentIdentityContext currentIdentity,
        IPermissionResolver permissionResolver,
        IUserRepository users,
        IPolicyCatalog policies,
        IAuthorizationSubjectAccessor subjects)
    {
        _currentUser = currentUser;
        _currentIdentity = currentIdentity;
        _permissionResolver = permissionResolver;
        _users = users;
        _policies = policies;
        _subjects = subjects;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;

        var permissions = await GetPermissionsAsync(cancellationToken);
        return permissions.Contains(permission, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return [];
        }

        if (_currentUser.Permissions.Count > 0)
        {
            return _currentUser.Permissions;
        }

        var stateName = _currentIdentity.AccessState;
        var roleName = _currentUser.Role;

        if (!string.IsNullOrWhiteSpace(stateName) &&
            !string.IsNullOrWhiteSpace(roleName) &&
            Enumeration.TryFromName<AccessState>(stateName, out var state) && state is not null &&
            Enumeration.TryFromName<UserRole>(roleName, out var role) && role is not null)
        {
            return _permissionResolver.Resolve(role, state);
        }

        if (_currentIdentity.UserId is null)
        {
            return [];
        }

        var user = await _users.GetByIdAsync(_currentIdentity.UserId, cancellationToken);
        return user is null ? [] : _permissionResolver.Resolve(user.Role, user.AccessState);
    }

    /// <summary>
    /// Whether the caller satisfies a named authorisation policy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Resolved through the policy catalogue since BSEC-003 (10 Nov 2026). It used to be
    /// <c>HasPermissionAsync(policy)</c>, and that could only ever return false.</b> A policy name
    /// is dotted (<c>automation.automations.read</c>) and a permission key is colon-separated
    /// (<c>automation:automations:read</c>) — deliberately, so that, as
    /// <c>AutomationPermissions</c> puts it, "a policy name and the permission behind it can never
    /// be confused in an attribute or a token claim". Treating one as the other asked whether the
    /// caller held a permission nobody has ever been granted.
    /// </para>
    /// <para>
    /// Nothing had noticed because nothing used it: the only caller is
    /// <c>AuthorizationBehavior</c>'s <c>Policy</c> arm, and no request type set that field. The
    /// 54 <c>[RequirePolicy(…)]</c> attributes in the repository are all on MVC controllers,
    /// where ASP.NET Core resolves the policy itself and gets the right answer. So this was a
    /// loaded trap rather than a live defect — the first request type to name a policy would have
    /// been refused for every caller, and the refusal would have looked exactly like a permission
    /// the member genuinely lacked.
    /// </para>
    /// <para>
    /// It matters now because BSEC-003's classification puts the route's own policy on the request
    /// behind it. Restating the same name is what keeps the two from drifting; it only works if
    /// the name means the same thing on both sides, which is what this method now guarantees.
    /// </para>
    /// <para>
    /// <b>An undeclared policy is refused, not ignored.</b> A typo, or a policy whose module is
    /// not composed in this host, yields no definition — and a check nobody can satisfy is the
    /// safe answer for a gate. ASP.NET Core does the same with an unregistered policy name.
    /// </para>
    /// </remarks>
    public async Task<bool> AuthorizeAsync(string policy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            return false;
        }

        // By canonical name or by any alias, which is what makes renaming a policy inert.
        var definition = _policies.Find(policy);

        if (definition is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(definition.Permission))
        {
            return await HasPermissionAsync(definition.Permission, cancellationToken);
        }

        if (definition.MinimumTier is not { } minimumTier)
        {
            // A definition with neither is rejected by the catalogue at startup, so this is
            // unreachable; refusing rather than passing keeps it unreachable AND harmless.
            return false;
        }

        var subject = _subjects.Current;

        // The same two exclusions AccessTierAuthorizationHandler applies, and for the same
        // reasons: a restricted account never satisfies a tier gate however high its token
        // claims to sit, and Anonymous is not a tier anything is granted at.
        if (subject.IsRestricted || subject.Tier == AccessTier.Anonymous)
        {
            return false;
        }

        return definition.RequiresExactTier
            ? subject.Tier == minimumTier
            : subject.Tier >= minimumTier;
    }
}

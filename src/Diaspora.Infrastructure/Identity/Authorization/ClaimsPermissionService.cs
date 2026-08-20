using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
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
/// Policies are expressed as permissions rather than as a separate concept. A second
/// vocabulary for the same decision would only create somewhere for the two to disagree.
/// </para>
/// </summary>
public sealed class ClaimsPermissionService : IPermissionService
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IUserRepository _users;

    public ClaimsPermissionService(
        ICurrentUser currentUser,
        ICurrentIdentityContext currentIdentity,
        IPermissionResolver permissionResolver,
        IUserRepository users)
    {
        _currentUser = currentUser;
        _currentIdentity = currentIdentity;
        _permissionResolver = permissionResolver;
        _users = users;
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

    public Task<bool> AuthorizeAsync(string policy, CancellationToken cancellationToken = default) =>
        HasPermissionAsync(policy, cancellationToken);
}

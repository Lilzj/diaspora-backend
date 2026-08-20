using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Resolves the permission set for a role at a given access state.
/// <para>
/// Both inputs matter: a Verified member and a member in limited access hold the same
/// role but must not hold the same permissions. This is what lets authorisation checks
/// name permissions instead of hardcoding role comparisons.
/// </para>
/// </summary>
public interface IPermissionResolver
{
    IReadOnlyList<string> Resolve(UserRole role, AccessState accessState);
}

using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Administration;

/// <summary>
/// Which role changes need a second member of staff.
/// <para>
/// Granting Admin or SuperAdmin does (the owner's rule, 7 Nov 2026): those are the roles that can
/// close accounts and appoint staff, so one person must not be able to hand them out alone. Every
/// other change of role is made at once, with a reason, a record and a notice to the member.
/// </para>
/// </summary>
public static class RoleGrants
{
    /// <summary>The role with this name, ignoring case; null for a name the platform does not have.</summary>
    public static UserRole? Parse(string? name) =>
        UserRole.All.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool NeedsSecondStaff(UserRole role) =>
        role == UserRole.Admin || role == UserRole.SuperAdmin;

    /// <summary>False for a name the platform does not have: that is refused elsewhere, by name.</summary>
    public static bool NeedsSecondStaff(string? roleName) =>
        Parse(roleName) is { } role && NeedsSecondStaff(role);
}

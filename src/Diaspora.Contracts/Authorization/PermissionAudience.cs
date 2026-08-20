namespace Diaspora.Contracts.Authorization;

/// <summary>
/// Who a permission can be issued to.
/// </summary>
public enum PermissionAudience
{
    /// <summary>Resolved from a member's tier and role.</summary>
    Member = 0,

    /// <summary>
    /// Only ever placed on a service token. No tier and no role resolves to one of these,
    /// which is the mechanism behind the Identity/Trust separation in ADR-002.
    /// </summary>
    Service = 1
}

namespace Diaspora.Contracts.Authorization;

/// <summary>
/// Everything the permission and feature catalogues need to know about a caller.
/// <para>
/// Deliberately a flat, module-free projection. Identity builds one of these from its
/// <c>User</c> aggregate; a background job or a service credential can build one without
/// an aggregate at all. Nothing downstream of this type knows what an access state is.
/// </para>
/// </summary>
/// <param name="Tier">Where the subject sits on the platform access ladder.</param>
/// <param name="RoleName">
/// The subject's role. Matched by name against a permission's role set, so a module can
/// grant to "Moderator" without referencing Identity's role enumeration.
/// </param>
/// <param name="IsStaff">Whether the role is a Diaspora staff role.</param>
/// <param name="IsRestricted">
/// Whether the account is under an enforcement action — suspended, locked or closed. A
/// restricted subject keeps only the capabilities explicitly marked as surviving
/// restriction, whatever their tier or role.
/// </param>
public sealed record AuthorizationSubject(
    AccessTier Tier,
    string RoleName,
    bool IsStaff,
    bool IsRestricted)
{
    /// <summary>An unauthenticated caller. Holds nothing.</summary>
    public static readonly AuthorizationSubject Anonymous =
        new(AccessTier.Anonymous, string.Empty, IsStaff: false, IsRestricted: false);
}

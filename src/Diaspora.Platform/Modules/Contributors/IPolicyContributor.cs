using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Modules.Contributors;

/// <summary>
/// Implemented by a module to declare the authorisation policies its endpoints reference.
/// <para>
/// The host turns each declaration into a registered ASP.NET Core policy, so a module never
/// touches <c>AddAuthorization</c> and the composition root never learns a module's policy
/// names.
/// </para>
/// </summary>
public interface IPolicyContributor
{
    /// <summary>The owning module's name.</summary>
    string Module { get; }

    /// <summary>
    /// Every policy this module declares. Names must be unique across the platform, and
    /// any permission a policy references must be declared by some
    /// <see cref="IPermissionContributor"/> — the kernel validates both at startup, so a
    /// typo in a policy name fails the boot rather than silently denying every request.
    /// </summary>
    IEnumerable<PolicyDefinition> GetPolicies();
}

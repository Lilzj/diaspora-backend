using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Api.Platform.Operations;

/// <summary>
/// The permissions behind the Production Platform's operator surface.
/// <para>
/// Three, split by what the caller can do rather than by which endpoint they call. Reading the
/// dashboards during an incident should be available to anybody on call; changing a feature flag or a
/// runtime setting is a production change; rotating a secret is a security operation whose blast radius
/// is every session on the platform.
/// </para>
/// </summary>
public static class OperationsPermissions
{
    /// <summary>Read the health model, the dashboards, the policy catalogue, the alert rules, the workers.</summary>
    public const string Read = "platform:operations:read";

    /// <summary>
    /// Change a feature flag, a runtime setting, a circuit breaker or a cache. Every one of these is a
    /// production change and every one of them is audited.
    /// </summary>
    public const string Manage = "platform:operations:manage";

    /// <summary>
    /// Rotate a secret.
    /// <para>
    /// Separate from <see cref="Manage"/> because the blast radius is different in kind. Rotating the
    /// access-token signing key with too short a retirement window signs every member out at once, and
    /// that is not a mistake somebody tuning a cache expiry should be one endpoint away from making.
    /// </para>
    /// </summary>
    public const string Secrets = "platform:operations:secrets";
}

/// <summary>The policy names the operational endpoints reference.</summary>
public static class OperationsPolicies
{
    public const string Read = "platform.operations.read";

    public const string Manage = "platform.operations.manage";

    public const string Secrets = "platform.operations.secrets";
}

/// <summary>
/// Declares the operator surface's permissions and policies.
/// <para>
/// <b>No tier grants any of them.</b> <c>MinimumTier</c> is null throughout and the role set is
/// explicit, so no amount of progression through the access tiers reaches an operational endpoint. That
/// is the same decision the outbox console made, for the same reason: engaging a kill switch or
/// isolating a payment provider is not a capability a member earns by verifying their identity.
/// </para>
/// </summary>
public sealed class OperationsAuthorizationContributor : IPermissionContributor, IPolicyContributor
{
    private static readonly IReadOnlySet<string> AdminAndAbove =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.Admin),
            nameof(UserRole.SuperAdmin)
        };

    private static readonly IReadOnlySet<string> SuperAdminOnly =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.SuperAdmin)
        };

    public string Module => "Platform.Operations";

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            OperationsPermissions.Read,
            Module,
            "Read the platform health model, operational dashboards, policies, alert rules and workers.")
        {
            MinimumTier = null,
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            OperationsPermissions.Manage,
            Module,
            "Change feature flags, runtime configuration, circuit breakers and cache state.")
        {
            MinimumTier = null,
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            OperationsPermissions.Secrets,
            Module,
            "Rotate platform secrets.")
        {
            MinimumTier = null,
            Roles = SuperAdminOnly
        };
    }

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            OperationsPolicies.Read, Module, OperationsPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            OperationsPolicies.Manage, Module, OperationsPermissions.Manage);

        yield return PolicyDefinition.ForPermission(
            OperationsPolicies.Secrets, Module, OperationsPermissions.Secrets);
    }
}

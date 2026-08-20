using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// Declares the permissions and policies guarding the outbox's operator surface.
/// <para>
/// Contributed through the ordinary interfaces rather than registered specially, which is worth
/// noting because the outbox is platform infrastructure rather than a module: the contributor
/// model does not care where a contribution comes from, and infrastructure that needed a
/// bespoke registration path would be evidence the model had a hole in it.
/// </para>
/// <para>
/// <b>No tier grants either permission.</b> <c>MinimumTier</c> is null and the role set is
/// explicit, so these are held by administrators and by nobody else — no amount of progression
/// through the access tiers reaches them. Replaying or skipping a message changes what other
/// modules believe happened, and that is not a capability a member earns by verifying their
/// identity.
/// </para>
/// </summary>
public sealed class OutboxAuthorizationContributor : IPermissionContributor, IPolicyContributor
{
    private static readonly IReadOnlySet<string> AdminAndAbove =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.Admin),
            nameof(UserRole.SuperAdmin)
        };

    public string Module => "Platform.Outbox";

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            OutboxPermissions.Read,
            Module,
            "Inspect the outbox: depth, pending messages, dead letters and delivery attempts.")
        {
            MinimumTier = null,
            Roles = AdminAndAbove
        };

        yield return new PermissionDefinition(
            OutboxPermissions.Manage,
            Module,
            "Replay dead letters, skip poison messages, force retries and purge delivered records.")
        {
            MinimumTier = null,
            Roles = AdminAndAbove
        };
    }

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            OutboxPolicies.Read, Module, OutboxPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            OutboxPolicies.Manage, Module, OutboxPermissions.Manage);
    }
}

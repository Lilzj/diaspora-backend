using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Notifications.Authorization;

/// <summary>
/// The named policies this module's endpoints reference.
/// <para>
/// Three, not one per endpoint. A policy is the endpoint-level gate that refuses before a body is
/// read; the permission attribute on the command is the one that refuses after the request is
/// understood. The three here are the three audiences: the member, the template author, the operator.
/// </para>
/// <para>
/// <b>Each carries its pre-B1.4.1 name as an alias.</b> The kernel registers both with ASP.NET Core
/// from this one definition, so the two names cannot come to mean different things. See
/// <see cref="NotificationPolicies.Legacy"/>.
/// </para>
/// </summary>
public sealed class NotificationPolicyContributor : IPolicyContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition
            .ForPermission(NotificationPolicies.Recipient, Module, NotificationPermissions.InboxRead)
            .WithAliases(NotificationPolicies.Legacy.Recipient);

        yield return PolicyDefinition
            .ForPermission(
                NotificationPolicies.TemplateAuthor, Module, NotificationPermissions.TemplatesManage)
            .WithAliases(NotificationPolicies.Legacy.TemplateAuthor);

        yield return PolicyDefinition
            .ForPermission(NotificationPolicies.Operator, Module, NotificationPermissions.DeliveryRead)
            .WithAliases(NotificationPolicies.Legacy.Operator);
    }
}

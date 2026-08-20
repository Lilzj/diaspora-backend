using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Identity's published facts become messages.
/// <para>
/// <b>The access-state rule is the archetypal Critical one and the reason the priority exists.</b> A
/// member whose account has just been restricted must be told whatever their quiet hours say and
/// whatever they have muted — because the alternative is discovering it when something they were
/// relying on stops working, with no explanation and no route to appeal.
/// </para>
/// </summary>
public sealed class IdentityNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "platform.welcome",
            EventName = typeof(UserRegisteredIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.Welcome,
            CategoryKey = NotificationCategoryKeys.Platform,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp],

            // Never expires. It is the first thing in a member's inbox and the only place the
            // platform introduces itself.
            RetentionPeriod = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "security.access.changed",
            EventName = typeof(UserAccessStateChangedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.AccessStateChanged,
            CategoryKey = NotificationCategoryKeys.Security,
            Priority = NotificationPriority.Critical,
            Channels =
            [
                NotificationChannelKind.InApp,
                NotificationChannelKind.Push,
                NotificationChannelKind.Email
            ],

            // No de-duplication. Every movement along the access ladder is genuinely news, and two in
            // an hour means something is happening the member needs to see both halves of.
            DeduplicationWindow = null
        };
    }
}

using Diaspora.Contracts.Intelligence.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How a published official notice reaches the members it concerns.
/// <para>
/// <b>Added 8 Oct 2026.</b> Every published notice raised <c>OfficialNoticePublished</c> and nothing
/// consumed it, so a notice a platform admin published for Mauritius reached nobody — and the app's
/// only list of notices was three screens deep. The owner decided notices go to every member living
/// in the notice's country, in the inbox and by push.
/// </para>
/// <para>
/// Filed under Alerts, so a member who mutes alerts is not sent them. Not digested: a notice about a
/// deadline is worth less in tomorrow's summary.
/// </para>
/// </summary>
public sealed class IntelligenceNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "intelligence.notice.published",
            EventName = typeof(OfficialNoticePublishedIntegrationEvent).FullName!,
            Audience = "country-audience",
            TemplateKey = NotificationTemplateKeys.OfficialNoticePublished,
            CategoryKey = NotificationCategoryKeys.Alerts,
            Priority = NotificationPriority.High,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],
            DeduplicationWindow = TimeSpan.FromHours(12)
        };
    }
}

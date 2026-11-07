using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Notifications.Application.Consumers.Moderation;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// Who is told about a moderation decision, and how (7 Nov 2026).
/// <para>
/// <b>The owner is always told, by email too.</b> The owner's rule for staff power: every time staff
/// act on a member's content, the member hears what happened, the reason, the moderator's note and
/// what they can do next. Taking something down is Critical, the one priority that reaches email
/// whatever the member's switches say (email is off by default). Putting it back is High and on the
/// account-mail list, so it reaches email too.
/// </para>
/// <para>
/// Reporters hear the outcome, "action taken" or "no action", in the app and by push, with no detail
/// about the owner or the decision.
/// </para>
/// <para>
/// Each notice's subject is the decision, not the content, so two decisions on the same advert in an
/// hour are two notices: the default de-duplication keys on the subject.
/// </para>
/// </summary>
public sealed class ModerationCaseNotificationRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] Everywhere =
    [
        NotificationChannelKind.InApp,
        NotificationChannelKind.Push,
        NotificationChannelKind.Email
    ];

    private static readonly NotificationChannelKind[] InAppAndPush =
    [
        NotificationChannelKind.InApp,
        NotificationChannelKind.Push
    ];


    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        var eventName = typeof(ModerationActionTakenIntegrationEvent).FullName!;

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseHidden,
            EventName = eventName,
            Audience = ModerationActionTakenConsumer.OwnerHidden,
            TemplateKey = NotificationTemplateKeys.ModerationCaseHidden,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseHiddenAutomatically,
            EventName = eventName,
            Audience = ModerationActionTakenConsumer.OwnerHiddenAutomatically,
            TemplateKey = NotificationTemplateKeys.ModerationCaseHiddenAutomatically,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseRemoved,
            EventName = eventName,
            Audience = ModerationActionTakenConsumer.OwnerRemoved,
            TemplateKey = NotificationTemplateKeys.ModerationCaseRemoved,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseRestored,
            EventName = eventName,
            Audience = ModerationActionTakenConsumer.OwnerRestored,
            TemplateKey = NotificationTemplateKeys.ModerationCaseRestored,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.ModerationReportOutcome,
            EventName = eventName,
            Audience = ModerationActionTakenConsumer.Reporter,
            TemplateKey = NotificationTemplateKeys.ModerationReportOutcome,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush,
            DeduplicationWindow = null
        };

        // Appeal outcomes: the member hears the answer and the reviewer's explanation, by email
        // too (7 Nov 2026: nobody was told; the answer sat on their timeline).
        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.AppealUpheld,
            EventName = typeof(Diaspora.Contracts.Trust.V1.Events.AppealApprovedIntegrationEvent).FullName!,
            Audience = "appellant",
            TemplateKey = NotificationTemplateKeys.AppealUpheld,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = NotificationTemplateKeys.AppealRefused,
            EventName = typeof(Diaspora.Contracts.Trust.V1.Events.AppealRejectedIntegrationEvent).FullName!,
            Audience = "appellant",
            TemplateKey = NotificationTemplateKeys.AppealRefused,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            Channels = Everywhere,
            DeduplicationWindow = null
        };
    }
}

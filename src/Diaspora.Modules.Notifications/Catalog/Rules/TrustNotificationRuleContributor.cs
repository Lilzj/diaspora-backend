using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Trust's published facts become messages.
/// <para>
/// <b>Every urgency decision in this file is visible as a value and changeable as a row.</b> That a
/// verification approval is High and a badge is Low is a product judgement, not a fact about the
/// code — and an operator who disagrees changes a rule rather than waiting for a deployment.
/// </para>
/// <para>
/// Verification outcomes are not digested. Somebody who has been waiting three weeks to hear whether
/// they can work should not learn it in tomorrow morning's summary.
/// </para>
/// </summary>
public sealed class TrustNotificationRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] Everywhere =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push, NotificationChannelKind.Email];

    private static readonly NotificationChannelKind[] InAppAndPush =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push];

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "trust.verification.approved",
            EventName = typeof(VerificationApprovedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.VerificationApproved,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            Channels = Everywhere,
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "trust.verification.rejected",
            EventName = typeof(VerificationRejectedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.VerificationRejected,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            Channels = Everywhere,
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "trust.verification.expired",
            EventName = typeof(VerificationExpiredIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.VerificationExpired,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "trust.verification.revoked",
            EventName = typeof(VerificationRevokedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.VerificationRevoked,
            CategoryKey = NotificationCategoryKeys.Trust,

            // Critical, and the only Trust rule that is. A revocation removes capabilities the member
            // was using an hour ago, and quiet hours holding it until morning would mean discovering
            // it by being refused something.
            Priority = NotificationPriority.Critical,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "trust.level.changed",
            EventName = typeof(TrustLevelChangedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.TrustLevelChanged,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,
            DeduplicationWindow = TimeSpan.FromHours(6)
        };

        yield return new NotificationRuleSeed
        {
            Key = "trust.badge.awarded",
            EventName = typeof(BadgeAwardedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.BadgeAwarded,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = InAppAndPush
        };
    }
}

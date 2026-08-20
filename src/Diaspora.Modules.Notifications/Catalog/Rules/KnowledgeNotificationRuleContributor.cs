using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Guides', the Knowledge Directory's and Providers' published facts become messages.
/// <para>
/// <b>The two country rules reach nobody today, and they are seeded anyway.</b> Their audience comes
/// from <c>INotificationAudienceResolver</c>, whose shipped implementation answers "nobody" because
/// no published contract says who lives where. Seeding them means the template exists, the delivery
/// path is exercised, and registering an audience adapter later is the only change — rather than a
/// phase that has to build the rules, the templates and the adapter at once.
/// </para>
/// </summary>
public sealed class KnowledgeNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "guides.featured",
            EventName = typeof(GuideFeaturedIntegrationEvent).FullName!,

            // B13.1 moved this from "editor" to "author". The rule used to address the staff
            // member who featured the guide, because the contract named nobody else; it now
            // addresses the person whose guide was featured, which is who the message was always
            // written for.
            Audience = "author",
            TemplateKey = NotificationTemplateKeys.GuideFeatured,
            CategoryKey = NotificationCategoryKeys.Guides,

            // Raised from Low and off the digest for the same reason: it is now genuinely good
            // news for the recipient rather than a curation receipt for the person who did it.
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push]
        };

        yield return new NotificationRuleSeed
        {
            Key = "learning.guide.featured",
            EventName = typeof(KnowledgeGuideFeaturedIntegrationEvent).FullName!,
            Audience = "country-audience",
            TemplateKey = NotificationTemplateKeys.KnowledgeGuideFeatured,
            CategoryKey = NotificationCategoryKeys.Learning,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(12)
        };

        yield return new NotificationRuleSeed
        {
            Key = "learning.path.updated",
            EventName = typeof(LearningPathUpdatedIntegrationEvent).FullName!,
            Audience = "country-audience",
            TemplateKey = NotificationTemplateKeys.LearningPathUpdated,
            CategoryKey = NotificationCategoryKeys.Learning,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(12)
        };

        yield return new NotificationRuleSeed
        {
            Key = "provider.review.published",
            EventName = typeof(ProviderReviewReceivedIntegrationEvent).FullName!,
            Audience = "reviewer",
            TemplateKey = NotificationTemplateKeys.ProviderReviewPublished,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp]
        };

        yield return new NotificationRuleSeed
        {
            Key = "provider.recommendation.published",
            EventName = typeof(ProviderRecommendationReceivedIntegrationEvent).FullName!,
            Audience = "recommender",
            TemplateKey = NotificationTemplateKeys.ProviderRecommendationPublished,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp]
        };

        // B13.1. The owner side of both facts — the client's original "New review from Amara K.",
        // deliverable for the first time now that the Providers contracts name the business's
        // owner rather than only the member who wrote about it.
        yield return new NotificationRuleSeed
        {
            Key = "provider.review.received",
            EventName = typeof(ProviderReviewReceivedIntegrationEvent).FullName!,
            Audience = "owner",
            TemplateKey = NotificationTemplateKeys.ProviderReviewReceived,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push]
        };

        yield return new NotificationRuleSeed
        {
            Key = "provider.recommendation.received",
            EventName = typeof(ProviderRecommendationReceivedIntegrationEvent).FullName!,
            Audience = "owner",
            TemplateKey = NotificationTemplateKeys.ProviderRecommendationReceived,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp]
        };
    }
}

using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for Guides', the Knowledge Directory's and Providers' messages.
/// <para>
/// <b>The country templates are the brief's "You're new to Canada — start with these guides", and
/// they are written as a statement of fact rather than a recommendation.</b> The directory decided
/// this guide is worth featuring, at this position, with this note; this module repeats the decision
/// and forms none. That is the whole difference between consuming published knowledge and
/// implementing a recommendation engine.
/// </para>
/// </summary>
public sealed class KnowledgeNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.GuideFeatured,
            CategoryKey = NotificationCategoryKeys.Guides,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/guides/{guideid}",
            Description = "An editor featured a guide.",
            Variables =
            [
                new TemplateVariableSeed { Name = "guideid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A guide was featured",
                    Body = "A guide has been featured on the platform.",
                    ShortBody = "A guide was featured."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.FollowedAuthorGuidePublished,
            CategoryKey = NotificationCategoryKeys.Following,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/guide/{guideid}",
            Description = "A member you follow published a new guide.",
            Variables =
            [
                new TemplateVariableSeed { Name = "authorname", Sample = "Amara Kone" },
                new TemplateVariableSeed { Name = "title", Sample = "Opening a bank account in Lyon" },
                new TemplateVariableSeed { Name = "guideid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New guide from {authorname}",
                    Body = "{authorname} published a new guide: {title}",
                    ShortBody = "{authorname} published a new guide."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.KnowledgeGuideFeatured,
            CategoryKey = NotificationCategoryKeys.Learning,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/knowledge/{countrycode}",
            Description = "A guide was featured on one country's knowledge directory.",
            Variables =
            [
                new TemplateVariableSeed { Name = "countrycode", Sample = "CA" },
                new TemplateVariableSeed { Name = "guideid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "note", IsRequired = false }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New guide for {countrycode}",
                    Body = "A new guide has been featured on the {countrycode} directory. {note}",
                    ShortBody = "New featured guide for {countrycode}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.LearningPathUpdated,
            CategoryKey = NotificationCategoryKeys.Learning,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/learning/{learningpathkey}",
            Description = "A country's learning path changed.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Settling in Canada" },
                new TemplateVariableSeed { Name = "countrycode", Sample = "CA" },
                new TemplateVariableSeed
                {
                    Name = "stepcount", Format = TemplateVariable.NumberFormat, Sample = "7"
                },
                new TemplateVariableSeed { Name = "learningpathkey", Sample = "settling-in-canada" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{title} was updated",
                    Body = "The {title} learning path now has {stepcount} step.",
                    PluralBody = "The {title} learning path now has {stepcount} steps.",
                    ShortBody = "{title} was updated."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProviderReviewPublished,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/providers/{providerid}",
            Description = "A member's review of a provider was published.",
            Variables =
            [
                new TemplateVariableSeed { Name = "providerid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "rating", Format = TemplateVariable.NumberFormat, Sample = "5"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your review was published",
                    Body = "Thank you. Your {rating}-star review is now visible to the community.",
                    ShortBody = "Your review was published."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProviderRecommendationPublished,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/providers/{providerid}",
            Description = "A member's recommendation of a provider was published.",
            Variables =
            [
                new TemplateVariableSeed { Name = "providerid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "kind", Sample = "Recommend" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your recommendation was published",
                    Body = "Thank you. Your {kind} is now visible to the community.",
                    ShortBody = "Your recommendation was published."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProviderReviewReceived,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/providers/{providerid}/reviews",
            Description = "Tells a provider's owner they received a review. B13.1.",
            Variables =
            [
                new TemplateVariableSeed { Name = "providerid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "rating", Format = TemplateVariable.NumberFormat, Sample = "5"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",

                    // The rating travels; the review text does not. A one-star review quoted in a
                    // push notification is the platform delivering an insult rather than a fact,
                    // and the owner's own screen is where the words belong.
                    Title = "New review",
                    Body = "Your business received a {rating}-star review.",
                    ShortBody = "New {rating}-star review."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProviderRecommendationReceived,
            CategoryKey = NotificationCategoryKeys.Reviews,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/providers/{providerid}",
            Description = "Tells a provider's owner somebody vouched for them. B13.1.",
            Variables =
            [
                new TemplateVariableSeed { Name = "providerid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "kind", Sample = "Recommend" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Somebody recommended you",
                    Body = "A member of the community published a {kind} for your business.",
                    ShortBody = "Somebody recommended you."
                }
            ]
        };
    }
}

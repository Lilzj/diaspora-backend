using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for an official notice reaching a member. The notice's own title and summary are
/// the message; the platform adds only the frame, so what the admin wrote is what members read.
/// </summary>
public sealed class IntelligenceNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.OfficialNoticePublished,
            CategoryKey = NotificationCategoryKeys.Alerts,
            Priority = NotificationPriority.High,
            RouteTemplate = "/intelligence/notice/{officialnoticeid}",
            Description = "An official notice was published for the member's country.",
            Variables =
            [
                new TemplateVariableSeed { Name = "officialnoticeid", Sample = "01a11959-3e02-70df-acfa-c97bf3eaeab0" },
                new TemplateVariableSeed { Name = "title", Sample = "International Passport Renewal" },
                new TemplateVariableSeed { Name = "summary", Sample = "You can now renew your international passport from Mauritius" },
                new TemplateVariableSeed { Name = "countrycode", Sample = "MU" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Official notice: {title}",
                    Body = "{summary}",
                    ShortBody = "{title}"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Avis officiel : {title}",
                    Body = "{summary}",
                    ShortBody = "{title}"
                }
            ]
        };
    }
}

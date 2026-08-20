using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for the platform's own messages: the welcome, and the security warning.
/// <para>
/// <b>The access-state template is the most consequential text in the module.</b> It is Critical, it
/// bypasses every preference, and it is the only thing a suspended member receives. It says what
/// happened and where to go, and it deliberately does not say why — a reason written by a moderator
/// is for a moderator, and a member deserves the appeal route rather than a paraphrase of a case file.
/// </para>
/// </summary>
public sealed class PlatformNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.Welcome,
            CategoryKey = NotificationCategoryKeys.Platform,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/profile",
            Description = "The first message in a new member's inbox.",
            Variables =
            [
                new TemplateVariableSeed { Name = "fullname", Sample = "Adaeze" },
                new TemplateVariableSeed
                {
                    Name = "countryofresidence", IsRequired = false, Sample = "Canada"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Welcome to Diaspora, {fullname}",
                    Body = "You are all set. Explore guides, jobs, housing and community near you, and verify your account to unlock more.",
                    ShortBody = "Welcome to Diaspora, {fullname}."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Bienvenue sur Diaspora, {fullname}",
                    Body = "Tout est prêt. Découvrez les guides, les emplois, les logements et la communauté près de chez vous, et vérifiez votre compte pour en débloquer davantage.",
                    ShortBody = "Bienvenue sur Diaspora, {fullname}."
                }
            ],
            Actions =
            [
                new TemplateActionSeed
                {
                    Key = "verify",
                    Label = "Verify your account",
                    RouteTemplate = "/verification/country"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.AccessStateChanged,
            CategoryKey = NotificationCategoryKeys.Security,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/profile/trust-center",
            Description = "An account was suspended, restricted, disabled or reinstated.",

            // Never expires. The one message a member must still be able to find months later,
            // because it is the record of what happened to their account.
            RetentionPeriod = null,
            Variables =
            [
                new TemplateVariableSeed { Name = "currentstate", Sample = "Suspended" },
                new TemplateVariableSeed { Name = "previousstate", IsRequired = false }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your account status changed",
                    Body = "Your account is now {currentstate}. Open your Trust Centre to see what this means and how to respond.",
                    ShortBody = "Your account is now {currentstate}."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Le statut de votre compte a changé",
                    Body = "Votre compte est maintenant {currentstate}. Ouvrez votre centre de confiance pour savoir ce que cela signifie et comment réagir.",
                    ShortBody = "Votre compte est maintenant {currentstate}."
                }
            ],
            Actions =
            [
                new TemplateActionSeed
                {
                    Key = "view",
                    Label = "Open Trust Centre",
                    RouteTemplate = "/profile/trust-center"
                }
            ]
        };
    }
}

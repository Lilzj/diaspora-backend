using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for Trust's messages.
/// <para>
/// <b>Two languages ship, and the second one is not decoration.</b> A platform serving diasporas
/// whose members read French as a first language and English as a second needs the fallback chain
/// exercised in production from day one — a chain nothing uses is a chain nobody notices is broken.
/// Everything else falls back to English, which is honest: a wrong translation of "your verification
/// was rejected" is worse than an English one.
/// </para>
/// <para>
/// Every variable is declared, so publication refuses a template referencing one it does not have —
/// which is what stops a member receiving a message with a literal <c>{verificationType}</c> in it.
/// </para>
/// </summary>
public sealed class TrustNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VerificationApproved,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            RouteTemplate = "/profile/trust-center",
            Description = "Trust approved a member's verification.",
            Variables =
            [
                new TemplateVariableSeed { Name = "verificationtype", Sample = "Identity" },
                new TemplateVariableSeed { Name = "targetcountry", IsRequired = false, Sample = "GB" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Verification approved",
                    Body = "Your {verificationtype} verification has been approved. New capabilities are now unlocked on your account.",
                    ShortBody = "Your {verificationtype} verification was approved."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Vérification approuvée",
                    Body = "Votre vérification {verificationtype} a été approuvée. De nouvelles fonctionnalités sont maintenant disponibles sur votre compte.",
                    ShortBody = "Votre vérification {verificationtype} a été approuvée."
                }
            ],
            // Verification results go by email even with email switched off (AccountMail, 7 Oct
            // 2026), so this is often the first email a member gets after the welcome. It arrives
            // without the app around it, so it says what happened and what is now open to them.
            Variants =
            [
                new TemplateVariantSeed
                {
                    Channel = NotificationChannelKind.Email,
                    Language = "en",
                    Title = "You are verified on Diaspora",
                    Body =
                        "Good news: your {verificationtype} verification has been approved.\n\n"
                        + "Your account now has full community access. Open the Diaspora app to "
                        + "post, sell, list a property and message other members."
                },
                new TemplateVariantSeed
                {
                    Channel = NotificationChannelKind.Email,
                    Language = "fr",
                    Title = "Vous êtes vérifié sur Diaspora",
                    Body =
                        "Bonne nouvelle : votre vérification {verificationtype} a été approuvée.\n\n"
                        + "Votre compte a désormais un accès complet à la communauté. Ouvrez "
                        + "l'application Diaspora pour publier, vendre, proposer un logement et "
                        + "contacter les autres membres."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VerificationRejected,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            RouteTemplate = "/profile/trust-center",
            Description = "Trust rejected a member's verification.",
            Variables =
            [
                new TemplateVariableSeed { Name = "verificationtype", Sample = "Identity" },
                new TemplateVariableSeed { Name = "reasoncode", Sample = "DocumentUnreadable" },
                // What the reviewer wrote under "What the member is told". Optional, and empty
                // when they wrote nothing.
                new TemplateVariableSeed { Name = "details", IsRequired = false, Sample = "The photo of the document is blurred." }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Verification could not be approved",
                    Body = "Your {verificationtype} verification was not approved. Open your Trust Centre to see what to do next.",
                    ShortBody = "Your {verificationtype} verification was not approved."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Vérification non approuvée",
                    Body = "Votre vérification {verificationtype} n'a pas été approuvée. Ouvrez votre centre de confiance pour connaître la suite.",
                    ShortBody = "Votre vérification {verificationtype} n'a pas été approuvée."
                }
            ],
            Variants =
            [
                new TemplateVariantSeed
                {
                    Channel = NotificationChannelKind.Email,
                    Language = "en",
                    Title = "Your Diaspora verification was not approved",
                    Body =
                        "We reviewed your {verificationtype} verification and could not approve it.\n\n"
                        + "{details}\n\n"
                        + "Open your Trust Centre in the Diaspora app to see the reason, then send "
                        + "it again or appeal the decision."
                },
                new TemplateVariantSeed
                {
                    Channel = NotificationChannelKind.Email,
                    Language = "fr",
                    Title = "Votre vérification Diaspora n'a pas été approuvée",
                    Body =
                        "Nous avons examiné votre vérification {verificationtype} et n'avons pas pu l'approuver.\n\n"
                        + "{details}\n\n"
                        + "Ouvrez votre centre de confiance dans l'application Diaspora pour en "
                        + "connaître la raison, puis renvoyez-la ou faites appel de la décision."
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

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VerificationExpired,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.High,
            RouteTemplate = "/profile/trust-center",
            Description = "A granted verification lapsed because its evidence expired.",
            Variables =
            [
                new TemplateVariableSeed { Name = "verificationtype", Sample = "Residency" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Verification has expired",
                    Body = "Your {verificationtype} verification has expired. Renew it to keep the capabilities it unlocked.",
                    ShortBody = "Your {verificationtype} verification has expired."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VerificationRevoked,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/profile/trust-center",
            Description = "A granted verification was withdrawn for cause.",
            Variables =
            [
                new TemplateVariableSeed { Name = "verificationtype", Sample = "Identity" },
                new TemplateVariableSeed { Name = "reason", IsRequired = false }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Verification withdrawn",
                    Body = "Your {verificationtype} verification has been withdrawn. Open your Trust Centre for the reason and how to appeal.",
                    ShortBody = "Your {verificationtype} verification was withdrawn."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.TrustLevelChanged,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/profile/trust-center",
            Description = "A member's trust level moved in either direction.",
            Variables =
            [
                new TemplateVariableSeed { Name = "currentlevel", Sample = "Trusted" },
                new TemplateVariableSeed { Name = "previouslevel", IsRequired = false }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your trust level is now {currentlevel}",
                    Body = "Your trust level changed from {previouslevel} to {currentlevel}. Your Trust Centre explains what that means.",
                    ShortBody = "Your trust level is now {currentlevel}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.BadgeAwarded,
            CategoryKey = NotificationCategoryKeys.Trust,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/profile/trust-center",
            Description = "Trust awarded a badge.",
            Variables =
            [
                new TemplateVariableSeed { Name = "badge", Sample = "Verified Resident" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You earned a badge",
                    Body = "You have been awarded the {badge} badge.",
                    ShortBody = "You earned the {badge} badge."
                }
            ]
        };
    }
}

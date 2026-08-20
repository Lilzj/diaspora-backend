using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for Marketplace's and Housing's messages.
/// <para>
/// <b>The money templates declare the currency format and never pre-format.</b> An amount arrives as
/// "62000 GBP" and renders with the reader's own grouping while keeping the seller's currency —
/// because the amount is a fact about the listing and the punctuation is a fact about the reader.
/// Formatting a British price with a naira sign because the reader is Nigerian would be far worse
/// than an unfamiliar separator.
/// </para>
/// </summary>
public sealed class CommerceNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.MarketplaceOfferReceived,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/marketplace/{listingid}",
            Description = "Somebody made an offer on a member's listing.",
            Variables =
            [
                new TemplateVariableSeed { Name = "listingtitle", Sample = "Toyota Corolla 2018" },
                new TemplateVariableSeed
                {
                    Name = "amount", Format = TemplateVariable.CurrencyFormat, Sample = "7500 GBP"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New offer on your listing",
                    Body = "You have an offer of {amount} on \"{listingtitle}\".",
                    ShortBody = "New offer of {amount} on \"{listingtitle}\"."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.MarketplaceOfferAccepted,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.High,
            RouteTemplate = "/marketplace/{listingid}",
            Description = "A seller accepted a member's offer.",
            Variables =
            [
                new TemplateVariableSeed { Name = "listingtitle", Sample = "Toyota Corolla 2018" },
                new TemplateVariableSeed
                {
                    Name = "amount", Format = TemplateVariable.CurrencyFormat, Sample = "7500 GBP"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your offer was accepted",
                    Body = "Your offer of {amount} for \"{listingtitle}\" was accepted. Arrange collection with the seller.",
                    ShortBody = "Your offer for \"{listingtitle}\" was accepted."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.MarketplaceReservationConfirmed,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.High,
            RouteTemplate = "/marketplace/{listingid}",
            Description = "A buyer reserved stock.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "quantity", Format = TemplateVariable.NumberFormat, Sample = "2"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Reservation confirmed",
                    Body = "You have reserved {quantity} item. The seller has been told.",

                    // The plural form: chosen when the count is not one. Everything the module does
                    // about pluralisation is these two strings.
                    PluralBody = "You have reserved {quantity} items. The seller has been told.",
                    ShortBody = "Reservation confirmed."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.HousingViewingScheduled,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/housing/{listingid}",
            Description = "A landlord agreed a time to show a property.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "scheduledfor",
                    Format = TemplateVariable.DateTimeFormat,
                    Sample = "Saturday 16 March at 11:00"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Viewing confirmed",
                    Body = "Your viewing is confirmed for {scheduledfor}, your local time. Open the listing for the address.",
                    ShortBody = "Viewing confirmed for {scheduledfor}."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Visite confirmée",
                    Body = "Votre visite est confirmée pour le {scheduledfor}, à votre heure locale. Ouvrez l'annonce pour l'adresse.",
                    ShortBody = "Visite confirmée pour le {scheduledfor}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.HousingViewingReminder,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/housing/{listingid}",
            Description = "Sent three hours before a viewing.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "scheduledfor", Format = TemplateVariable.TimeFormat, Sample = "11:00"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Viewing today",
                    Body = "Your viewing is at {scheduledfor}. Open the listing for the address and directions.",
                    ShortBody = "Viewing today at {scheduledfor}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.HousingReservationConfirmed,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/housing/{listingid}",
            Description = "A seeker reserved a room or a unit.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "units", Format = TemplateVariable.NumberFormat, Sample = "1"
                },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Reservation confirmed",
                    Body = "Your reservation is confirmed. The landlord has been told.",
                    ShortBody = "Housing reservation confirmed."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.HousingReservationCancelled,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/housing/{listingid}",
            Description = "A housing reservation ended.",
            Variables =
            [
                new TemplateVariableSeed { Name = "outcome", Sample = "Withdrawn" },
                new TemplateVariableSeed { Name = "listingid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Reservation ended",
                    Body = "Your housing reservation has ended ({outcome}). The listing is open again if you want to look.",
                    ShortBody = "Your housing reservation has ended."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.HousingLandlordVerified,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/profile",
            Description = "A landlord's verification was granted.",
            Variables =
            [
                new TemplateVariableSeed { Name = "landlordname", Sample = "Woolwich Lets" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You are verified as a landlord",
                    Body = "{landlordname} is now a verified landlord. Your listings will show the verified badge.",
                    ShortBody = "{landlordname} is now verified."
                }
            ]
        };
    }
}

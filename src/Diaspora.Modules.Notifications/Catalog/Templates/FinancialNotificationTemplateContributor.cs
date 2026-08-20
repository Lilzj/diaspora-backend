using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for the Financial Platform's messages.
/// <para>
/// <b>Every amount is declared with the currency format and none is pre-formatted</b>, which is the
/// convention the commerce templates established and which matters more here than anywhere else on
/// the platform. An amount arrives as "1200 NGN" and renders with the reader's own grouping while
/// keeping the currency the transaction actually happened in. A receipt that silently restated a
/// naira donation in pounds because the reader's phone is British would be a false financial record.
/// </para>
/// <para>
/// <b>No template states a balance.</b> B15 owns five distinct balances — available, pending,
/// reserved, lifetime credits, lifetime debits — precisely because "you have £40" and "£40 of which
/// £15 is held against a pending capture" are different sentences and only one is honest. A
/// notification that picked one would be picking the dishonest one, so every template links to the
/// wallet instead of quoting it.
/// </para>
/// </summary>
public sealed class FinancialNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.PaymentFailed,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/wallet/payments/{paymentintentid}",
            Description = "A payment attempt failed.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "amount", Format = TemplateVariable.CurrencyFormat, Sample = "1200 NGN"
                },

                // The provider's own words, passed through rather than mapped to a platform
                // vocabulary. "Insufficient funds" and "card expired" call for different actions and
                // a platform that flattened both to "payment failed" would make the member guess.
                new TemplateVariableSeed { Name = "reason", Sample = "Insufficient funds" },
                new TemplateVariableSeed { Name = "paymentintentid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Payment failed",
                    Body = "Your payment of {amount} did not go through: {reason}. Nothing was charged.",
                    ShortBody = "Your payment of {amount} did not go through."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.RefundIssued,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/wallet/payments/{paymentintentid}",
            Description = "A refund was issued against a payment.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "amount", Format = TemplateVariable.CurrencyFormat, Sample = "1200 NGN"
                },
                new TemplateVariableSeed { Name = "reason", Sample = "Order cancelled" },
                new TemplateVariableSeed { Name = "paymentintentid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Refund issued",
                    Body = "A refund of {amount} is on its way back to you. Reason: {reason}.",
                    ShortBody = "A refund of {amount} is on its way back to you."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.SubscriptionActivated,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/wallet/subscription",
            Description = "A subscription became active.",
            Variables =
            [
                new TemplateVariableSeed { Name = "tier", Sample = "Premium" },
                new TemplateVariableSeed { Name = "plancode", Sample = "premium-monthly" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{tier} is active",
                    Body = "Your {tier} plan is active. Everything it includes is available now.",
                    ShortBody = "Your {tier} plan is active."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.SubscriptionEnded,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/wallet/subscription",
            Description = "A subscription ended, lapsed or was cancelled.",
            Variables =
            [
                new TemplateVariableSeed { Name = "tier", Sample = "Premium" },
                new TemplateVariableSeed { Name = "plancode", Sample = "premium-monthly" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {tier} plan has ended",

                    // States what is true and does not sell. A member whose card expired and a member
                    // who cancelled deliberately both read this, and the second one being pitched to
                    // is how a billing category gets muted.
                    Body = "Your {tier} plan has ended. Your account and everything in it are unchanged.",
                    ShortBody = "Your {tier} plan has ended."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.PlacementActivated,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/wallet/placements/{placementid}",
            Description = "A purchased placement went live.",
            Variables =
            [
                new TemplateVariableSeed { Name = "kind", Sample = "Boost" },
                new TemplateVariableSeed { Name = "placementid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {kind} is live",
                    Body = "Your {kind} is now running and will show in search and recommendations until it ends.",
                    ShortBody = "Your {kind} is now live."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.PlacementEnded,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/wallet/placements/{placementid}",
            Description = "A purchased placement finished.",
            Variables =
            [
                new TemplateVariableSeed { Name = "kind", Sample = "Boost" },
                new TemplateVariableSeed { Name = "placementid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {kind} has finished",
                    Body = "Your {kind} has finished and is no longer affecting where your listing appears.",
                    ShortBody = "Your {kind} has finished."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.DonationReceipt,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/wallet/receipts/{receiptreference}",
            Description = "A completed donation, receipted to the donor.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "amount", Format = TemplateVariable.CurrencyFormat, Sample = "5000 NGN"
                },
                new TemplateVariableSeed { Name = "receiptreference", Sample = "DN-2026-000481" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Thank you for your donation",
                    Body = "Your donation of {amount} is complete. Your receipt reference is {receiptreference}.",
                    ShortBody = "Your donation of {amount} is complete."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ContributionReceipt,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/wallet/receipts/{receiptreference}",
            Description = "A completed community contribution, receipted to the contributor.",
            Variables =
            [
                // Not a currency variable, and that is the interesting one. Three of B15's five
                // contribution kinds are non-monetary — a pledge of goods, of time, of a skill — so
                // this renders "5000 NGN" for one and "12 hours" for another. A currency format
                // would have to invent a currency for the pledges that have none.
                new TemplateVariableSeed { Name = "contribution", Sample = "12 hours" },
                new TemplateVariableSeed { Name = "fundcode", Sample = "lagos-flood-relief" },
                new TemplateVariableSeed { Name = "receiptreference", Sample = "CN-2026-001204" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Thank you for your contribution",
                    Body = "Your contribution of {contribution} to {fundcode} is recorded. Your receipt reference is {receiptreference}.",
                    ShortBody = "Your contribution to {fundcode} is recorded."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.WalletStatusChanged,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            RouteTemplate = "/wallet",
            Description = "A member's wallet was frozen, closed or reactivated.",
            Variables =
            [
                new TemplateVariableSeed { Name = "status", Sample = "Frozen" },
                new TemplateVariableSeed { Name = "reason", Sample = "Under review" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your wallet is now {status}",
                    Body = "Your wallet status changed to {status}. Reason: {reason}.",
                    ShortBody = "Your wallet is now {status}."
                }
            ]
        };
    }
}

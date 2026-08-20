using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;

// Two modules publish a contract with this name; the alias names the one this file means, which is
// the same collision the rules themselves key on the namespace-qualified name to avoid.
using HousingReservationCancelled = Diaspora.Contracts.Housing.V1.Events.ReservationCancelledIntegrationEvent;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Marketplace's and Housing's published facts become messages.
/// <para>
/// <b>One contributor for both, because the collision is the interesting part.</b> Marketplace and
/// Housing each publish a <c>ReservationCancelledIntegrationEvent</c>, and Jobs and Marketplace each
/// publish an <c>OfferAcceptedIntegrationEvent</c>. Rules key on the namespace-qualified name, which
/// is why they cannot cross — and having the two sets in one file is what makes that visible to the
/// next person editing them.
/// </para>
/// </summary>
public sealed class CommerceNotificationRuleContributor : INotificationRuleContributor
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
            Key = "marketplace.offer.received",
            EventName = typeof(OfferCreatedIntegrationEvent).FullName!,
            Audience = "seller",
            TemplateKey = NotificationTemplateKeys.MarketplaceOfferReceived,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,

            // Short window: a listing attracting six offers in an evening should read as one
            // conversation, not six interruptions. The consumer groups them on the listing.
            DeduplicationWindow = TimeSpan.FromMinutes(15)
        };

        yield return new NotificationRuleSeed
        {
            Key = "marketplace.offer.accepted",
            EventName = typeof(OfferAcceptedIntegrationEvent).FullName!,
            Audience = "buyer",
            TemplateKey = NotificationTemplateKeys.MarketplaceOfferAccepted,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "marketplace.reservation.confirmed",
            EventName = typeof(ListingReservedIntegrationEvent).FullName!,
            Audience = "buyer",
            TemplateKey = NotificationTemplateKeys.MarketplaceReservationConfirmed,
            CategoryKey = NotificationCategoryKeys.Marketplace,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "housing.viewing.scheduled",
            EventName = typeof(ViewingScheduledIntegrationEvent).FullName!,
            Audience = "seeker",
            TemplateKey = NotificationTemplateKeys.HousingViewingScheduled,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "housing.viewing.reminder",
            EventName = typeof(ViewingScheduledIntegrationEvent).FullName!,
            Audience = "seeker-reminder",
            TemplateKey = NotificationTemplateKeys.HousingViewingReminder,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.Critical,
            Policy = DeliveryPolicy.Scheduled,

            // Three hours before. A viewing is local and short-notice in a way an interview is not,
            // and a day-before reminder is one somebody has already forgotten by the morning.
            Offset = TimeSpan.FromHours(-3),
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "housing.reservation.confirmed",
            EventName = typeof(HousingReservedIntegrationEvent).FullName!,
            Audience = "seeker",
            TemplateKey = NotificationTemplateKeys.HousingReservationConfirmed,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "housing.reservation.cancelled",
            EventName = typeof(HousingReservationCancelled).FullName!,
            Audience = "seeker",
            TemplateKey = NotificationTemplateKeys.HousingReservationCancelled,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "housing.landlord.verified",
            EventName = typeof(LandlordVerifiedIntegrationEvent).FullName!,
            Audience = "landlord-verified",
            TemplateKey = NotificationTemplateKeys.HousingLandlordVerified,
            CategoryKey = NotificationCategoryKeys.Housing,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };
    }
}

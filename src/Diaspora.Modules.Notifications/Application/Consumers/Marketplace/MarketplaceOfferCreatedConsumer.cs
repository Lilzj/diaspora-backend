using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Marketplace;

/// <summary>
/// Somebody made an offer on a listing.
/// <para>
/// Named <c>MarketplaceOfferCreatedConsumer</c> rather than <c>OfferCreatedConsumer</c> because Jobs
/// also has offers, and two consumers with one name in one assembly is a rename waiting to be
/// resolved by whoever is on call. The published contract keeps its own name; this module
/// disambiguates on its own side, which is where the collision is.
/// </para>
/// <para>
/// The seller is told. The buyer already knows — they just made it.
/// </para>
/// </summary>
public sealed class MarketplaceOfferCreatedConsumer
    : ModuleEventConsumer<OfferCreatedIntegrationEvent>
{
    public MarketplaceOfferCreatedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Marketplace";

    protected override IEnumerable<NotificationTrigger> Translate(
        OfferCreatedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["listingTitle"] = integrationEvent.ListingTitle;
        values["amount"] = $"{integrationEvent.Amount} {integrationEvent.CurrencyCode}";
        values["quantity"] = integrationEvent.Quantity;
        values["listingId"] = integrationEvent.ListingId;

        yield return Trigger(
            UserId.From(integrationEvent.SellerUserId),
            "seller",
            integrationEvent.CreatedAt,
            values,
            subjectType: "listing",
            subjectId: integrationEvent.ListingId.ToString(),

            // Grouped on the listing: a listing attracting six offers in an evening should read as
            // one conversation, not six interruptions.
            groupKey: $"marketplace:offers:{integrationEvent.ListingId}");
    }
}

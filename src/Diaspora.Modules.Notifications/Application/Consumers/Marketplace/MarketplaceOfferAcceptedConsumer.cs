using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Marketplace;

/// <summary>
/// An offer was accepted.
/// <para>
/// The buyer is told, and this is the one marketplace message with money and a stranger's address at
/// the end of it — which is why the rule gives it a higher urgency than the offer itself.
/// </para>
/// </summary>
public sealed class MarketplaceOfferAcceptedConsumer
    : ModuleEventConsumer<OfferAcceptedIntegrationEvent>
{
    public MarketplaceOfferAcceptedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Marketplace";

    protected override IEnumerable<NotificationTrigger> Translate(
        OfferAcceptedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["listingTitle"] = integrationEvent.ListingTitle;
        values["amount"] = $"{integrationEvent.Amount} {integrationEvent.CurrencyCode}";
        values["quantity"] = integrationEvent.Quantity;
        values["listingId"] = integrationEvent.ListingId;

        yield return Trigger(
            UserId.From(integrationEvent.BuyerUserId),
            "buyer",
            integrationEvent.AcceptedAt,
            values,
            subjectType: "listing",
            subjectId: integrationEvent.ListingId.ToString());
    }
}

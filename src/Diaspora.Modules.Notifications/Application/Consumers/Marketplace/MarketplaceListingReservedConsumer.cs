using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Marketplace;

/// <summary>
/// A buyer reserved stock.
/// <para>
/// The reservation confirmation the brief asks for. It carries the quantity and what is left, so the
/// template can say "two of the four remaining", which is the difference between a member turning up
/// and a member wondering whether they need to.
/// </para>
/// </summary>
public sealed class MarketplaceListingReservedConsumer
    : ModuleEventConsumer<ListingReservedIntegrationEvent>
{
    public MarketplaceListingReservedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Marketplace";

    protected override IEnumerable<NotificationTrigger> Translate(
        ListingReservedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["quantity"] = integrationEvent.Quantity;
        values["remainingQuantity"] = integrationEvent.RemainingQuantity;
        values["listingId"] = integrationEvent.ListingId;

        yield return Trigger(
            UserId.From(integrationEvent.BuyerUserId),
            "buyer",
            integrationEvent.ReservedAt,
            values,
            subjectType: "listing",
            subjectId: integrationEvent.ListingId.ToString(),
            count: integrationEvent.Quantity);
    }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Observability;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Units changed hands: publish the contract, count it, and tidy up after it.
/// <para>
/// <b>The tidying is the part that matters to real people.</b> A listing that has sold out leaves
/// buyers with open negotiations they will never get an answer to and open conversations about an
/// item that is gone. Expiring the negotiations and closing the conversation references is the
/// difference between a marketplace that ends transactions and one that leaves people waiting.
/// </para>
/// <para>
/// Only a full sell-out settles the open offers. A listing of six chairs that sold two still has
/// four for the people negotiating over them.
/// </para>
/// </summary>
public sealed class ListingSoldEventHandler : DomainEventHandler<ListingSoldDomainEvent>
{
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingOfferRepository _offers;
    private readonly IListingConversationRepository _conversations;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly ConversationBroker _broker;
    private readonly IGuidGenerator _guids;

    public ListingSoldEventHandler(
        MarketplaceIntegrationEventRelay relay,
        ISellerProfileRepository sellers,
        IListingOfferRepository offers,
        IListingConversationRepository conversations,
        IListingAnalyticsRepository analytics,
        ConversationBroker broker,
        IGuidGenerator guids)
    {
        _relay = relay;
        _sellers = sellers;
        _offers = offers;
        _conversations = conversations;
        _analytics = analytics;
        _broker = broker;
        _guids = guids;
    }

    protected override async Task HandleAsync(
        ListingSoldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        MarketplaceTelemetry.ItemsSold.Add(domainEvent.Quantity);

        if (domainEvent.TimeToSale is { } elapsed)
        {
            MarketplaceTelemetry.TimeToSaleDays.Record(elapsed.TotalDays);
        }

        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);
        seller?.RecordSale();

        var analytics = await _analytics.GetForListingAsync(
            domainEvent.ListingId, cancellationToken);

        // Recorded here only for a direct sale. A sale that came out of a reservation is recorded
        // by the reservation handler, and counting it twice would double every seller's figures.
        if (domainEvent.ReservationId is null)
        {
            analytics?.RecordSale(domainEvent.SoldAt, domainEvent.Quantity);
        }

        if (!domainEvent.IsSoldOut)
        {
            return;
        }

        await SettleOpenNegotiationsAsync(domainEvent, cancellationToken);
        await CloseConversationsAsync(domainEvent, cancellationToken);
    }

    /// <summary>
    /// Expires every open negotiation on a sold-out listing.
    /// <para>
    /// Expiry rather than rejection, because nobody decided against these buyers — the item simply
    /// went. The history keeps every round of what was said, and the buyer's dashboard stops showing
    /// a negotiation that is waiting on a seller who has nothing left to sell.
    /// </para>
    /// </summary>
    private async Task SettleOpenNegotiationsAsync(
        ListingSoldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var open = await _offers.GetOpenForListingAsync(domainEvent.ListingId, cancellationToken);

        foreach (var offer in open)
        {
            // The accepted negotiation that produced this sale, where there was one, has already
            // settled and is not in this list.
            offer.Withdraw(
                offer.SellerUserId,
                "The item sold.",
                domainEvent.SoldAt,
                _guids.NewSequentialGuid());
        }
    }

    private async Task CloseConversationsAsync(
        ListingSoldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var open = await _conversations.GetOpenForListingAsync(
            domainEvent.ListingId, cancellationToken);

        foreach (var conversation in open)
        {
            conversation.Close(domainEvent.SoldAt);

            // Best-effort, and only where a provider ever claimed the reference. On a deployment
            // with no messaging this is a no-op.
            await _broker.TryCloseAsync(conversation, cancellationToken);
        }
    }
}

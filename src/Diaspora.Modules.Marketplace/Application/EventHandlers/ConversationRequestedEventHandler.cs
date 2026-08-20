using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Offers a new conversation reference to whatever messaging provider is registered.
/// <para>
/// <b>On every deployment today there is none, and this handler does nothing.</b> That is the
/// shipped state and it is not a gap: the reference persists, the client renders the seller's
/// contact preference, and the day a Communication module registers a provider the same references
/// start being claimed without a line changing here.
/// </para>
/// <para>
/// A handler rather than an inline call in the command, because opening a conversation with an
/// external provider is a network call and a buyer pressing "message seller" should not wait on it
/// to get their reference back.
/// </para>
/// </summary>
public sealed class ConversationRequestedEventHandler
    : DomainEventHandler<ConversationRequestedDomainEvent>
{
    private readonly ConversationBroker _broker;
    private readonly IListingConversationRepository _conversations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;

    public ConversationRequestedEventHandler(
        ConversationBroker broker,
        IListingConversationRepository conversations,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers)
    {
        _broker = broker;
        _conversations = conversations;
        _listings = listings;
        _sellers = sellers;
    }

    protected override async Task HandleAsync(
        ConversationRequestedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!_broker.IsAvailable)
        {
            return;
        }

        var conversation = await _conversations.GetAsync(
            domainEvent.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var seller = await _sellers.GetAsync(domainEvent.SellerId, cancellationToken);

        await _broker.TryAttachAsync(
            conversation,
            listing?.Title.Value ?? string.Empty,
            seller?.Identity.DisplayName ?? string.Empty,
            cancellationToken);
    }
}

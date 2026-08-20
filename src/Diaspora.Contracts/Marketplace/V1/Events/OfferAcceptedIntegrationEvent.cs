using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A negotiation ended in agreement.
/// <para>
/// Published <em>as well as</em> the general settlement contract, so a consumer that only cares
/// about deals need not subscribe to every rejection, withdrawal and expiry on the platform to
/// find them. That is the same split B9 made between an offer being answered and an offer being
/// accepted, and for the same reason.
/// </para>
/// <para>
/// <b>Acceptance is not a sale.</b> A reservation is created in the same transaction and the sale
/// happens when it completes; a consumer that counted this as revenue would be counting buyers who
/// never turned up. <see cref="ReservationId"/> is the thread to follow.
/// </para>
/// </summary>
public sealed record OfferAcceptedIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid BuyerUserId { get; init; }

    /// <summary>The reservation the acceptance created, which is where the stock went.</summary>
    public Guid? ReservationId { get; init; }

    public required decimal Amount { get; init; }

    public required string CurrencyCode { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Which side accepted. Either may, and the notification goes to the other.</summary>
    public required string AcceptedByParty { get; init; }

    public Guid? AcceptedBy { get; init; }

    /// <summary>How many moves the negotiation took. A quality signal about the price guidance.</summary>
    public required int Rounds { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }
}

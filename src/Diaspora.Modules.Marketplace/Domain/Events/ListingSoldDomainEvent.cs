using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when units actually change hands.
/// <para>
/// <b>Separate from the status transition, because they are not the same fact.</b> A listing of
/// six identical chairs sells four of them without its status moving at all, and a consumer that
/// only watched the status would count one sale for the six. This is the event a seller's sold
/// count, the category's trend and the time-to-sale histogram are all computed from.
/// </para>
/// <para>
/// <see cref="ReservationId"/> is present when the sale came out of a reservation and null when
/// the seller recorded it directly — the difference between a hand-off the platform brokered and
/// one it merely heard about.
/// </para>
/// </summary>
public sealed record ListingSoldDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    /// <summary>Who bought, when the platform knows. Null on a directly recorded sale.</summary>
    public UserId? BuyerUserId { get; init; }

    /// <summary>The reservation the sale came out of, when there was one.</summary>
    public ListingReservationId? ReservationId { get; init; }

    public required int Quantity { get; init; }

    /// <summary>What was paid, if the platform was told. Never inferred from the asking price.</summary>
    public decimal? Amount { get; init; }

    public string? CurrencyCode { get; init; }

    /// <summary>Whether that was the last unit.</summary>
    public required bool IsSoldOut { get; init; }

    /// <summary>How long from first publication to this sale. The brief's time-to-sale.</summary>
    public TimeSpan? TimeToSale { get; init; }

    public required DateTimeOffset SoldAt { get; init; }
}

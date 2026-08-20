using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A hold ended without a sale — cancelled by a party, or lapsed.
/// <para>
/// <b>The stock has already gone back on the shelf by the time this is delivered.</b> That is the
/// point of publishing it: a discovery consumer showing "reserved" needs telling that the item is
/// buyable again, and a buyer who saved the listing wants to know it is back.
/// </para>
/// <para>
/// <see cref="WasExpired"/> separates the two endings because they say different things about the
/// people involved. A cancellation is somebody's decision; an expiry is nobody's, and holding it
/// against a seller's cancellation rate would penalise them for a buyer who never turned up.
/// </para>
/// </summary>
public sealed record ReservationCancelledIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Units available again after the release.</summary>
    public required int RemainingQuantity { get; init; }

    /// <summary>True when the clock ended it, false when a person did.</summary>
    public required bool WasExpired { get; init; }

    /// <summary>Who called it off. Null on an expiry.</summary>
    public Guid? CancelledBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }
}

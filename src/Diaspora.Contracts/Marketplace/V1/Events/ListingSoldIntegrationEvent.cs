using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// Units changed hands.
/// <para>
/// <b>The contract the whole commerce side exists to produce.</b> It is what a seller's sold count,
/// a category's trend, a member's purchase history and a Trust reputation signal are all computed
/// from — and it is the one event a future payments or escrow module will hang itself off without
/// this module changing.
/// </para>
/// <para>
/// <b>Published per sale, not per listing.</b> A listing of six identical chairs raises this four
/// times if four are sold; <see cref="IsSoldOut"/> says whether that was the last one. A consumer
/// that treated the event as "the listing is gone" would drop a listing that still has stock.
/// </para>
/// <para>
/// <b>The amount is what the parties agreed, and it is absent when the platform was not told.</b>
/// A hand-off arranged in person and recorded afterwards has no figure attached, and inferring one
/// from the asking price would put a number the platform invented into a purchase history.
/// </para>
/// </summary>
public sealed record ListingSoldIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>Who bought, when the platform knows. Null on a directly recorded sale.</summary>
    public Guid? BuyerUserId { get; init; }

    /// <summary>The reservation the sale came out of, when there was one.</summary>
    public Guid? ReservationId { get; init; }

    public required int Quantity { get; init; }

    /// <summary>What was paid, if the platform was told. Never inferred.</summary>
    public decimal? Amount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public string? CurrencyCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>Whether that was the last unit.</summary>
    public required bool IsSoldOut { get; init; }

    /// <summary>How long from first publication to this sale. The brief's time to sale.</summary>
    public TimeSpan? TimeToSale { get; init; }

    public required DateTimeOffset SoldAt { get; init; }
}

namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// What the seller is asking, as a buyer sees it.
/// <para>
/// <b>The seller's private minimum is deliberately absent.</b> A visible floor is just a lower
/// asking price, and publishing it would end negotiation on this platform overnight. It appears on
/// the seller's own view of their listing and nowhere else.
/// </para>
/// </summary>
public sealed record ListingPriceResponse
{
    /// <summary>False when the seller published no figure — a wanted advert with no budget.</summary>
    public required bool IsDisclosed { get; init; }

    public decimal? Amount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted, on any surface.</summary>
    public string? Currency { get; init; }

    public required bool IsNegotiable { get; init; }

    /// <summary>Whether the item is free. What the card renders as "Free" rather than "0".</summary>
    public required bool IsFree { get; init; }

    /// <summary>
    /// The floor the seller will entertain. Present only on the seller's own view of their own
    /// listing, and null on every buyer-facing response.
    /// </summary>
    public decimal? MinimumOffer { get; init; }
}

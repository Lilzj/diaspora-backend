namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// One listing as a result set renders it.
/// <para>
/// <b>Deliberately much smaller than <see cref="ListingResponse"/>.</b> A page of twenty cards
/// that carried twenty full listings would be several hundred kilobytes to render twenty titles
/// and twenty prices — and the repository's card graph exists precisely so the database is not
/// asked for the rest either.
/// </para>
/// </summary>
public sealed record ListingCardResponse
{
    public required Guid ListingId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string Summary { get; init; }

    public required string CategoryKey { get; init; }

    public required string ListingType { get; init; }

    public required string Condition { get; init; }

    public required string Status { get; init; }

    public required ListingPriceResponse Price { get; init; }

    /// <summary>Units a buyer could commit to. The card renders "1 left" from this.</summary>
    public required int Available { get; init; }

    public required ListingLocationResponse Location { get; init; }

    /// <summary>The card image, by reference. Null when the seller uploaded none.</summary>
    public Guid? CoverMediaId { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required SellerSummaryResponse Seller { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsVerified { get; init; }

    public required bool OffersDelivery { get; init; }

    public required bool OffersShipping { get; init; }

    public required long ViewCount { get; init; }

    public required long SaveCount { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Whether the caller has it in their watchlist. Null for an anonymous reader.</summary>
    public bool? IsSaved { get; init; }

    /// <summary>
    /// Distance from the viewer, when the search was scoped to a point. Null on a jurisdiction
    /// search, where a number would be an invention.
    /// </summary>
    public double? DistanceKm { get; init; }
}

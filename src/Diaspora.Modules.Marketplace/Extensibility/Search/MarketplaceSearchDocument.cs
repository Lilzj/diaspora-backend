namespace Diaspora.Modules.Marketplace.Extensibility.Search;

/// <summary>
/// One listing, flattened for whatever engine is behind the port.
/// <para>
/// <b>Flat primitives only — no domain types, no value objects, no enums.</b> An adapter for
/// OpenSearch, Meilisearch or a hosted service must be writable without referencing this module's
/// domain, and every field here maps to something an index actually stores. That constraint is
/// what makes "infrastructure changes only" true rather than aspirational.
/// </para>
/// <para>
/// <b><see cref="PriceAmount"/> is carried with its currency and is never normalised.</b> An engine
/// that sorted a mixed-currency result set by raw amount would rank 500 JPY above 400 EUR;
/// sorting by price is therefore only meaningful within a currency, and the module's own filter
/// enforces that. An engine that wants a comparable column may compute one, and it will visibly be
/// an approximation.
/// </para>
/// </summary>
public sealed record MarketplaceSearchDocument
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public string? Summary { get; init; }

    /// <summary>The full body, for an engine that indexes it. Large; an engine may ignore it.</summary>
    public string? Body { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>The category's materialised path, so subtree queries need no tree walk.</summary>
    public string? CategoryPath { get; init; }

    /// <summary>Normalised tag slugs.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Structured attributes as key/value pairs, for faceting and filtering.</summary>
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }

    public string? Brand { get; init; }

    public string? Model { get; init; }

    public int? Year { get; init; }

    /// <summary><c>FixedPrice</c>, <c>Negotiable</c>, <c>Wanted</c>, <c>Giveaway</c>, …</summary>
    public required string ListingType { get; init; }

    /// <summary><c>New</c>, <c>LikeNew</c>, <c>Good</c>, …</summary>
    public required string Condition { get; init; }

    public required bool IsPriceDisclosed { get; init; }

    public decimal? PriceAmount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public string? CurrencyCode { get; init; }

    public required bool IsNegotiable { get; init; }

    public required bool IsFree { get; init; }

    public required int AvailableQuantity { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? Neighbourhood { get; init; }

    /// <summary>Location's materialised area path, for prefix-scoped queries.</summary>
    public string? AreaPath { get; init; }

    public required bool OffersPickup { get; init; }

    public required bool OffersDelivery { get; init; }

    public required bool OffersShipping { get; init; }

    /// <summary>Whether the seller carried a live Trust verification when this was projected.</summary>
    public required bool IsSellerVerified { get; init; }

    /// <summary>The seller badge, as the module computes it. Rendered, never interpreted.</summary>
    public required string SellerType { get; init; }

    public required string SellerName { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>The card image, by reference. The engine resolves nothing; the client asks Media.</summary>
    public Guid? CoverMediaId { get; init; }

    public required long ViewCount { get; init; }

    public required long SaveCount { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

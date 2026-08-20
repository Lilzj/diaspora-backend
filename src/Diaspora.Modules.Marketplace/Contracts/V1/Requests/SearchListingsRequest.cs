namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// The whole search surface, as a bound query object.
/// <para>
/// <b>Every filter is optional, and the empty request is a valid browse.</b> A marketplace's front
/// door is "show me what is near me", not "type something"; a search API that required a term
/// would make the most common journey impossible to express.
/// </para>
/// <para>
/// <b>The price band travels with its currency, and the pair is enforced.</b> Nothing in this
/// module converts, so a bound without a currency would compare numbers in different units — and
/// a buyer filtering "under 200" across four currencies would get an answer that is wrong in three
/// of them.
/// </para>
/// </summary>
public sealed record SearchListingsRequest
{
    public string? Text { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>A category subtree — everything under Electronics. Matched as a path prefix.</summary>
    public string? CategoryPath { get; init; }

    public string? Tag { get; init; }

    /// <summary>
    /// Structured attribute filters, as <c>key:value</c> pairs. A listing must match every one —
    /// somebody asking for a 128GB black phone means both.
    /// </summary>
    public IReadOnlyList<string>? Attributes { get; init; }

    public string? Brand { get; init; }

    public string? Model { get; init; }

    /// <summary>
    /// <c>New</c>, <c>LikeNew</c>, <c>Excellent</c>, <c>Good</c>, <c>Fair</c> or <c>ForParts</c>.
    /// Read as a floor: conditions are ordered worst-to-best.
    /// </summary>
    public string? MinimumCondition { get; init; }

    public IReadOnlyList<string>? ListingTypes { get; init; }

    // ── Geography ───────────────────────────────────────────────────────────────────────────

    public string? CountryCode { get; init; }

    public string? City { get; init; }

    /// <summary>Location's area identifier, when the client picked one from the hierarchy.</summary>
    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The radius. Location's own sentinels are accepted and resolved by B4.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// A map viewport: <c>north,south,east,west</c>. Wins over the point-and-radius pair.
    /// <para>
    /// This module stores no coordinate for a listing — a second-hand item is at somebody's home
    /// — so B4 resolves the rectangle to the smallest catalogue subtree containing it and the
    /// listing's own area path is matched against that. A listing that ships is still never
    /// excluded by a geographic narrowing, viewport or otherwise.
    /// </para>
    /// </summary>
    public string? Bounds { get; init; }

    // ── Commerce ────────────────────────────────────────────────────────────────────────────

    public decimal? MinimumPrice { get; init; }

    public decimal? MaximumPrice { get; init; }

    /// <summary>ISO 4217. Required whenever a price bound is set.</summary>
    public string? Currency { get; init; }

    public bool FreeOnly { get; init; }

    public bool NegotiableOnly { get; init; }

    public bool DeliveryOnly { get; init; }

    public bool ShippingOnly { get; init; }

    // ── Who is selling ──────────────────────────────────────────────────────────────────────

    public Guid? SellerId { get; init; }

    public bool VerifiedSellersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    /// <summary><c>Individual</c> or <c>Business</c>.</summary>
    public IReadOnlyList<string>? SellerKinds { get; init; }

    // ── Order and paging ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>Relevance</c>, <c>Newest</c>, <c>PriceLowToHigh</c>, <c>PriceHighToLow</c>,
    /// <c>MostViewed</c>, <c>Featured</c>, <c>Nearest</c> or <c>EndingSoon</c>.
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>A ranking strategy key — <c>balanced</c>, <c>trusted</c>, <c>recency</c>.</summary>
    public string? Ranking { get; init; }

    /// <summary>The continuation from a previous page. Opaque.</summary>
    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    /// <summary>Whether to compute the facet counts. Off by default — they are not free.</summary>
    public bool IncludeFacets { get; init; }
}

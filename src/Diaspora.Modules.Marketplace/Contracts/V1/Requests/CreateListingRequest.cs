namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Everything needed to draft a listing.
/// <para>
/// <b>Enums arrive as strings and are parsed server-side.</b> A wire contract that sent integers
/// would break the moment somebody reordered an enum, and would be unreadable in a log. The
/// parser accepts the canonical name case-insensitively and refuses anything else.
/// </para>
/// <para>
/// Only the first block is required. A draft is allowed to be incomplete — a seller photographs
/// the item, thinks about the price, and comes back. Publication is where the listing has to be
/// able to answer everything.
/// </para>
/// </summary>
public sealed record CreateListingRequest
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>The card line. Derived from the description when omitted.</summary>
    public string? Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>ISO 3166-1 alpha-2. Case-insensitive.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// <c>FixedPrice</c>, <c>Negotiable</c>, <c>Wanted</c>, <c>Giveaway</c>,
    /// <c>BusinessInventory</c> or <c>Swap</c>. <c>Auction</c> is a recognised name and is refused.
    /// A swap states what it wants back as the <c>swap-wants</c> attribute, and any cash top-up as
    /// <c>swap-top-up</c> with the amount as the price.
    /// </summary>
    public string? ListingType { get; init; }

    /// <summary>
    /// <c>New</c>, <c>LikeNew</c>, <c>Excellent</c>, <c>Good</c>, <c>Fair</c>, <c>ForParts</c> or
    /// <c>NotApplicable</c>.
    /// </summary>
    public string? Condition { get; init; }

    // ── Price ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The asking price. Zero on a giveaway; null publishes no figure.</summary>
    public decimal? Price { get; init; }

    /// <summary>ISO 4217. Defaults to the country's currency when omitted.</summary>
    public string? Currency { get; init; }

    public bool IsNegotiable { get; init; }

    /// <summary>The floor the seller will entertain. Never rendered to a buyer.</summary>
    public decimal? MinimumOffer { get; init; }

    // ── Stock ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Units for sale. One when omitted, which is what a second-hand listing is.</summary>
    public int? Quantity { get; init; }

    // ── Place ───────────────────────────────────────────────────────────────────────────────

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>The seller's own words — "near the market". Never a Location entity.</summary>
    public string? Neighbourhood { get; init; }

    /// <summary>Location's area identifier, when the seller picked one from the hierarchy.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>
    /// The pin, in WGS84 degrees (-90..90). Optional; send both or neither.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>The pin, in WGS84 degrees (-180..180). Optional; send both or neither.</summary>
    public double? Longitude { get; init; }

    // ── Getting it to the buyer ─────────────────────────────────────────────────────────────

    public bool OffersPickup { get; init; } = true;

    public bool OffersDelivery { get; init; }

    public bool OffersShipping { get; init; }

    public bool IsDigital { get; init; }

    public int? DeliveryRadiusKm { get; init; }

    public string? FulfillmentNotes { get; init; }

    // ── The thing itself ────────────────────────────────────────────────────────────────────

    public string? Brand { get; init; }

    public string? Model { get; init; }

    /// <summary>The seller's own stock reference. Never interpreted.</summary>
    public string? Sku { get; init; }

    public int? Year { get; init; }

    public IReadOnlyList<ListingAttributeRequest>? Attributes { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    // ── The rest ────────────────────────────────────────────────────────────────────────────

    /// <summary><c>InApp</c>, <c>Phone</c>, <c>Email</c> or <c>Any</c>. In-app when omitted.</summary>
    public string? ContactPreference { get; init; }

    /// <summary><c>Public</c>, <c>Unlisted</c> or <c>Private</c>.</summary>
    public string? Visibility { get; init; }

    /// <summary>A preferred deep-link handle. Derived from the title when omitted or taken.</summary>
    public string? Slug { get; init; }

    /// <summary>When it should stop being offered. The deployment default applies when omitted.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Media identifiers to attach at once, cover first. Uploaded through B5 beforehand.</summary>
    public IReadOnlyList<Guid>? MediaIds { get; init; }
}

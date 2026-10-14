using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// Everything a listing query can narrow by, in one record.
/// <para>
/// <b>A record with <c>init</c> members rather than a fluent builder</b>, so a caller composes a
/// filter with <c>with</c> expressions and the repository receives one immutable object. That is
/// what lets the geographic adapter narrow a filter without knowing what else is on it.
/// </para>
/// <para>
/// <b>The three audience fields are the security-relevant ones.</b> <see cref="ViewerId"/>,
/// <see cref="ViewerSellerId"/> and <see cref="ViewerIsStaff"/> are what let drafts, unlisted and
/// hidden listings through, and they are applied in exactly one method in the repository. A
/// caller that forgets to set them gets the public view, which is the safe default — the failure
/// mode is a seller not seeing their own draft, not a stranger seeing it.
/// </para>
/// </summary>
public sealed record ListingQueryFilter
{
    // ── Audience ────────────────────────────────────────────────────────────────────────────

    /// <summary>Who is asking. Null is an anonymous browse.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>The viewer's own seller profile, when they have one.</summary>
    public SellerProfileId? ViewerSellerId { get; init; }

    /// <summary>Whether the viewer holds a platform staff role.</summary>
    public bool ViewerIsStaff { get; init; }

    /// <summary>
    /// Whether to include the viewer's own drafts, unlisted and archived listings. Only ever
    /// widens for listings the viewer's own seller profile owns.
    /// </summary>
    public bool IncludeNonPublic { get; init; }

    // ── Subject ─────────────────────────────────────────────────────────────────────────────

    public IReadOnlyCollection<MarketplaceListingId>? ListingIds { get; init; }

    public SellerProfileId? SellerId { get; init; }

    /// <summary>A Providers storefront. Narrows to the seller profiles that shop backs.</summary>
    public Guid? ProviderId { get; init; }

    public IReadOnlyCollection<ListingStatus>? Statuses { get; init; }

    /// <summary>Only listings a buyer could commit to. True on nearly every real browse.</summary>
    public bool AvailableOnly { get; init; }

    // ── Classification ──────────────────────────────────────────────────────────────────────

    public string? CategoryKey { get; init; }

    /// <summary>
    /// A category subtree, matched as a path prefix. What "everything under Electronics" is.
    /// </summary>
    public string? CategoryPathPrefix { get; init; }

    public string? Tag { get; init; }

    public IReadOnlyCollection<ListingType>? ListingTypes { get; init; }

    /// <summary>The worst condition acceptable. Conditions are ordered worst-to-best.</summary>
    public ProductCondition? MinimumCondition { get; init; }

    public string? Brand { get; init; }

    public string? Model { get; init; }

    /// <summary>
    /// Attribute filters as (key, value) pairs. A listing must match every pair — unlike tags,
    /// where any match qualifies: somebody asking for a 128GB <em>black</em> phone means both.
    /// </summary>
    public IReadOnlyCollection<KeyValuePair<string, string>>? Attributes { get; init; }

    // ── Geography ───────────────────────────────────────────────────────────────────────────

    public string? CountryCode { get; init; }

    /// <summary>A town, matched case-insensitively against the town the seller typed.</summary>
    public string? CityName { get; init; }

    /// <summary>
    /// A rectangle over the listing's own coordinates: a map viewport, or the rectangle around a
    /// circle search (14 Oct 2026). A listing with no coordinates is outside it unless it names
    /// <see cref="UnpinnedCityName"/>.
    /// </summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    /// <summary>
    /// With a rectangle: a town whose unpinned listings are kept, compared case-insensitively.
    /// Listings predate the pin, and a circle search that also named a town still finds them.
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>
    /// With a rectangle: whether a listing that ships is kept wherever it is. True for a circle —
    /// a buyer near Manchester can be posted to — and false for a map viewport, which asks only
    /// what is inside it.
    /// </summary>
    public bool IncludeShippingOutsideBox { get; init; }

    /// <summary>Whether all four edges of the rectangle are set.</summary>
    public bool HasBoundingBox =>
        MinLatitude is not null && MaxLatitude is not null
        && MinLongitude is not null && MaxLongitude is not null;

    /// <summary>Whether the rectangle wraps across ±180°.</summary>
    public bool BoundingBoxCrossesAntimeridian => HasBoundingBox && MinLongitude > MaxLongitude;

    /// <summary>Location's materialised area path, matched as a prefix.</summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// Paths a listing may be filed <em>above</em> the narrowed subtree and still match.
    /// <para>
    /// Set only by a map viewport. A second-hand item is at somebody's home and this module
    /// deliberately stores no coordinate for it, so a rectangle is answered as a catalogue
    /// subtree — and a listing filed against the city must survive somebody zooming into one
    /// street of it. An <c>IN</c> rather than a reversed <c>StartsWith</c>, which EF Core does
    /// not translate with a parameter receiver.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string>? AreaPathAncestors { get; init; }

    // ── Commerce ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The price floor, with its currency. Both or neither: nothing here converts, so a bound
    /// without a currency would compare numbers in different units.
    /// </summary>
    public decimal? MinimumPrice { get; init; }

    public decimal? MaximumPrice { get; init; }

    public string? Currency { get; init; }

    /// <summary>Free items only. The filter this platform's members use more than any other.</summary>
    public bool FreeOnly { get; init; }

    public bool NegotiableOnly { get; init; }

    public bool OffersDeliveryOnly { get; init; }

    public bool OffersShippingOnly { get; init; }

    /// <summary>Neither delivery nor shipping offered: the buyer collects.</summary>
    public bool PickupOnly { get; init; }

    // ── Trust & placement ───────────────────────────────────────────────────────────────────

    /// <summary>Only listings whose seller carries a live Trust verification.</summary>
    public bool VerifiedSellersOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    public IReadOnlyCollection<SellerKind>? SellerKinds { get; init; }

    // ── Moderation ──────────────────────────────────────────────────────────────────────────

    /// <summary>The moderation queue's own filter. Staff only; ignored otherwise.</summary>
    public bool ReportedOnly { get; init; }

    public ListingModerationState? ModerationState { get; init; }

    // ── Text, time & order ──────────────────────────────────────────────────────────────────

    public string? Text { get; init; }

    public DateTimeOffset? PublishedAfter { get; init; }

    public ListingSortOrder Sort { get; init; } = ListingSortOrder.Newest;

    public int Limit { get; init; } = MarketplaceLimits.DefaultPageSize;

    /// <summary>The page size actually used, clamped to the module's ceiling.</summary>
    public int EffectiveLimit => Math.Clamp(Limit, 1, MarketplaceLimits.MaxPageSize);
}

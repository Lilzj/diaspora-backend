namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// One listing in full.
/// <para>
/// <b>The viewer-relative flags at the bottom are what make the detail screen renderable in one
/// call.</b> Whether the caller may edit it, whether they have saved it, whether they already have
/// an offer standing — each is otherwise a separate round trip, and a client that made four of
/// them would render the page in four stages.
/// </para>
/// <para>
/// They are null rather than false for an anonymous reader, which is what lets the client render a
/// sign-in prompt rather than a wrong state.
/// </para>
/// </summary>
public sealed record ListingResponse
{
    public required Guid ListingId { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public required string Description { get; init; }

    public required string Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>The category's display name, resolved at read time.</summary>
    public string? CategoryName { get; init; }

    /// <summary>The category's materialised path, so a client can render the breadcrumb.</summary>
    public string? CategoryPath { get; init; }

    /// <summary>
    /// <c>FixedPrice</c>, <c>Negotiable</c>, <c>Wanted</c>, <c>Giveaway</c>, <c>Swap</c>, …
    /// </summary>
    public required string ListingType { get; init; }

    /// <summary>For a swap, what the seller would take in exchange. Null for every other type.</summary>
    public string? SwapWants { get; init; }

    /// <summary>For a donated item, the cause it goes to (16 Oct 2026). Null for anything else.</summary>
    public Guid? DonationCauseId { get; init; }

    /// <summary>The donated item's cause, by name.</summary>
    public string? DonationCauseTitle { get; init; }

    /// <summary>
    /// For a swap, <c>None</c>, <c>SellerAdds</c> or <c>BuyerAdds</c>; the amount is the price.
    /// Null for every other listing type.
    /// </summary>
    public string? SwapTopUp { get; init; }

    /// <summary><c>New</c>, <c>LikeNew</c>, <c>Good</c>, …</summary>
    public required string Condition { get; init; }

    /// <summary>
    /// <c>Draft</c>, <c>Published</c>, <c>Active</c>, <c>Reserved</c>, <c>Sold</c>,
    /// <c>Expired</c> or <c>Archived</c>.
    /// </summary>
    public required string Status { get; init; }

    public required string Visibility { get; init; }

    public required ListingPriceResponse Price { get; init; }

    public required ListingInventoryResponse Inventory { get; init; }

    public required ListingLocationResponse Location { get; init; }

    public required ListingFulfillmentResponse Fulfillment { get; init; }

    public required ProductIdentityResponse Product { get; init; }

    public required IReadOnlyList<ListingAttributeResponse> Attributes { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<ListingMediaResponse> Media { get; init; }

    public required IReadOnlyList<ListingProviderReferenceResponse> Providers { get; init; }

    public required IReadOnlyList<ListingGuideReferenceResponse> Guides { get; init; }

    public required SellerSummaryResponse Seller { get; init; }

    public required ListingEngagementResponse Engagement { get; init; }

    /// <summary>Trust's verdict on this particular listing, distinct from the seller's.</summary>
    public required ListingTrustResponse Trust { get; init; }

    /// <summary>How the seller would rather be contacted about this item.</summary>
    public required string ContactPreference { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? SoldAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The optimistic concurrency token. Sent back on the next edit.</summary>
    public required int Revision { get; init; }

    // ── Viewer-relative ─────────────────────────────────────────────────────────────────────

    /// <summary>Whether a buyer could commit to it right now.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Whether the seller is taking offers on it.</summary>
    public required bool AcceptsOffers { get; init; }

    /// <summary>Whether the seller is taking reservations on it.</summary>
    public required bool AcceptsReservations { get; init; }

    /// <summary>Whether the caller may edit and manage it. Null for an anonymous reader.</summary>
    public bool? CanManage { get; init; }

    /// <summary>Whether the caller has it in their watchlist.</summary>
    public bool? IsSaved { get; init; }

    /// <summary>The caller's open negotiation on it, when they have one.</summary>
    public Guid? MyOpenOfferId { get; init; }

    /// <summary>The caller's live reservation on it, when they have one.</summary>
    public Guid? MyReservationId { get; init; }

    /// <summary>The moderation state, on the seller's and staff's views only.</summary>
    public string? ModerationState { get; init; }

    /// <summary>How many members have reported it. Staff view only.</summary>
    public int? ReportCount { get; init; }
}

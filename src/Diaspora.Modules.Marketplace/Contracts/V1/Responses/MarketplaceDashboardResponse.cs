namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// The marketplace home surface, in one call.
/// <para>
/// <b>The buyer's counterpart to the seller dashboard, and it works for a member who has never
/// sold anything.</b> <see cref="Seller"/> is null for most people, and every field below it is
/// still populated — a marketplace whose home screen required a seller profile would be a
/// marketplace nobody could browse.
/// </para>
/// </summary>
public sealed record MarketplaceDashboardResponse
{
    /// <summary>The caller's own seller profile, when they have one.</summary>
    public SellerProfileResponse? Seller { get; init; }

    // ── The buyer's own state ───────────────────────────────────────────────────────────────

    /// <summary>Negotiations waiting on the caller as a buyer.</summary>
    public required int OffersAwaitingYou { get; init; }

    /// <summary>Items the caller is holding and has not collected.</summary>
    public required int ActiveReservations { get; init; }

    public required int SavedListings { get; init; }

    /// <summary>Saved listings whose price has come down since they were saved.</summary>
    public required int SavedListingsWithPriceDrops { get; init; }

    // ── Discovery ───────────────────────────────────────────────────────────────────────────

    /// <summary>The top of the category tree, for the browse tiles.</summary>
    public required IReadOnlyList<ListingCategoryResponse> TopCategories { get; init; }

    /// <summary>Promoted listings. Placement, never rank suppression.</summary>
    public required IReadOnlyList<ListingCardResponse> Featured { get; init; }

    /// <summary>Just listed, in the caller's own country.</summary>
    public required IReadOnlyList<ListingCardResponse> RecentlyAdded { get; init; }

    /// <summary>Near the caller, as Location resolved "near".</summary>
    public required IReadOnlyList<ListingCardResponse> Nearby { get; init; }

    /// <summary>
    /// The geography the nearby rail was scoped to, as Location resolved it. Null when the caller
    /// gave nothing to resolve.
    /// </summary>
    public string? ResolvedScope { get; init; }
}

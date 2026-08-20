namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// Everything a seller needs on opening the app, in one call.
/// <para>
/// <b>Assembled server-side deliberately.</b> A client that made six calls to build this screen
/// would render it in six stages on a slow connection, and would be six chances to get the
/// authorisation of each wrong. One handler, one authorisation decision, one payload.
/// </para>
/// <para>
/// The three "needs attention" numbers at the top are the ones that cost a seller money when they
/// are ignored: an unanswered offer expires, an unacknowledged reservation lapses, and a listing
/// about to expire is one that stops being seen tonight.
/// </para>
/// </summary>
public sealed record SellerDashboardResponse
{
    public required SellerProfileResponse Seller { get; init; }

    // ── Needs attention ─────────────────────────────────────────────────────────────────────

    /// <summary>Negotiations waiting on the seller. The number that costs them money.</summary>
    public required int OffersAwaitingYou { get; init; }

    /// <summary>Reservations the seller has not acknowledged.</summary>
    public required int ReservationsAwaitingYou { get; init; }

    /// <summary>Listings expiring within the next day.</summary>
    public required int ListingsExpiringSoon { get; init; }

    // ── Standing figures ────────────────────────────────────────────────────────────────────

    public required int ActiveListings { get; init; }

    public required int DraftListings { get; init; }

    public required int ReservedListings { get; init; }

    public required int SoldListings { get; init; }

    public required long TotalViews { get; init; }

    public required long TotalSaves { get; init; }

    // ── The work ────────────────────────────────────────────────────────────────────────────

    /// <summary>The negotiations waiting on the seller, most urgent first.</summary>
    public required IReadOnlyList<OfferCardResponse> PendingOffers { get; init; }

    /// <summary>The live reservations, soonest collection first.</summary>
    public required IReadOnlyList<ReservationResponse> ActiveReservations { get; init; }

    /// <summary>The seller's most recently touched listings.</summary>
    public required IReadOnlyList<ListingCardResponse> RecentListings { get; init; }
}

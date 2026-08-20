namespace Diaspora.Modules.Marketplace.Extensibility.Ranking;

/// <summary>
/// Everything a ranking strategy is allowed to know about one listing.
/// <para>
/// <b>Flat primitives, and a deliberately short list.</b> A strategy that received the aggregate
/// could reach into the seller, the offers and the analytics, and would become impossible to
/// reason about or to test. What is here is what a ranking can honestly use: how fresh it is, how
/// trusted the seller is, how people have engaged with it, and how far away it is.
/// </para>
/// </summary>
public sealed record ListingRankingContext
{
    public required Guid ListingId { get; init; }

    /// <summary>Hours since publication. The freshness signal every strategy uses.</summary>
    public required double AgeHours { get; init; }

    /// <summary>Whether the seller carries a live Trust verification.</summary>
    public required bool IsSellerVerified { get; init; }

    /// <summary>Whether a shop in the Providers directory stands behind the seller.</summary>
    public required bool IsStorefront { get; init; }

    /// <summary>Whether a live promotion grants featured placement.</summary>
    public required bool IsFeatured { get; init; }

    public required long ViewCount { get; init; }

    public required long SaveCount { get; init; }

    public required long OfferCount { get; init; }

    /// <summary>
    /// The seller's completed sales. A reputation signal the module can compute honestly, unlike a
    /// rating, which belongs to Community.
    /// </summary>
    public required int SellerCompletedSales { get; init; }

    /// <summary>
    /// The seller's cancellation rate, when they have enough history for it to mean anything.
    /// Null below five finished reservations.
    /// </summary>
    public double? SellerCancellationRate { get; init; }

    /// <summary>
    /// Distance from the viewer, when the search was scoped to a point. Null when it was not, and
    /// a strategy that assumed zero would rank every jurisdiction-scoped listing as "here".
    /// </summary>
    public double? DistanceKm { get; init; }

    /// <summary>The engine's relevance score, when the search had a term.</summary>
    public double? TextScore { get; init; }

    /// <summary>Whether the listing has a cover image. A listing without one converts far worse.</summary>
    public required bool HasCoverImage { get; init; }
}

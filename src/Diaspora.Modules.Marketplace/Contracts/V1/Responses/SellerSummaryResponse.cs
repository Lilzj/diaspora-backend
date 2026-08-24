namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// The seller as a listing card shows them.
/// <para>
/// <see cref="SellerType"/> is computed at read time from the declared kind, the live Trust
/// verification and the Providers link — never stored. A stored badge would keep saying "Verified
/// Business" after Trust revoked the verification, which is the platform vouching for exactly the
/// wrong party.
/// </para>
/// </summary>
public sealed record SellerSummaryResponse
{
    public required Guid SellerId { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    /// <summary>
    /// <c>Individual</c>, <c>VerifiedSeller</c>, <c>Business</c>, <c>VerifiedBusiness</c> or
    /// <c>ProviderStore</c>.
    /// </summary>
    public required string SellerType { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust's badge label, when there is one. Rendered, never interpreted.</summary>
    public string? Badge { get; init; }

    /// <summary>The shop in the Providers directory this storefront is, when it is one.</summary>
    public Guid? ProviderId { get; init; }

    /// <summary>The logo, by reference. Media owns the bytes.</summary>
    public Guid? LogoMediaId { get; init; }

    /// <summary>Completed sales. A reputation signal this module can compute honestly.</summary>
    public required int CompletedSales { get; init; }

    /// <summary>
    /// The share of finished reservations the seller called off. Null below five, because a seller
    /// who cancelled their only reservation is not a hundred-percent canceller.
    /// </summary>
    public double? CancellationRate { get; init; }

    /// <summary>Mean minutes to a first answer. Null until they have answered anything.</summary>
    public double? AverageResponseMinutes { get; init; }

    public required DateTimeOffset MemberSince { get; init; }

    /// <summary>
    /// The star rating, or <b>null when nobody has reviewed this seller</b>.
    /// <para>
    /// Null rather than a zeroed rollup, and the distinction is the point. Until this field
    /// existed the marketplace screen rendered
    /// <c>seller.rating != null ? seller.rating.toFixed(1) : '4.8'</c> — and since nothing ever
    /// sent a rating, every seller on the platform showed the same invented 4.8 directly above
    /// "Pay with Diaspora". A null that a client must handle is the only shape that cannot be
    /// quietly defaulted into a claim.
    /// </para>
    /// <para>
    /// Only a completed purchase can produce a review, so this is a rating of transactions
    /// rather than of opinions.
    /// </para>
    /// </summary>
    public SellerRatingResponse? Rating { get; init; }
}

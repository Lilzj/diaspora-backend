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
}

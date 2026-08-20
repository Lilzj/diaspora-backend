namespace Diaspora.Modules.Marketplace.Extensibility.Fraud;

/// <summary>
/// What a fraud provider is told about a listing.
/// <para>
/// <b>Flat primitives, and the seller's identity is a profile identifier rather than a member
/// one.</b> A provider needs to know that two listings share a seller; it does not need to know
/// which member that is, and handing it a <c>UserId</c> would put a personal identifier into every
/// third-party heuristic that ever ships.
/// </para>
/// </summary>
public sealed record FraudEvaluationContext
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public decimal? PriceAmount { get; init; }

    public string? CurrencyCode { get; init; }

    /// <summary>How many photographs the listing carries. Zero is itself a signal.</summary>
    public required int MediaCount { get; init; }

    /// <summary>How old the seller profile is, in days.</summary>
    public required int SellerAgeDays { get; init; }

    /// <summary>How many listings the seller already has live.</summary>
    public required int SellerActiveListings { get; init; }

    /// <summary>Whether the seller carries a live Trust verification.</summary>
    public required bool IsSellerVerified { get; init; }

    /// <summary>Why the evaluation is running — <c>publish</c>, <c>report</c>, <c>sweep</c>.</summary>
    public required string Trigger { get; init; }
}

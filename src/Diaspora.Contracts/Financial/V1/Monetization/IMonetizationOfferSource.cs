namespace Diaspora.Contracts.Financial.V1.Monetization;

/// <summary>
/// What a member could buy right now, and what they already hold — read by Recommendations to
/// suggest plans and promotions (MON-GAP-011).
/// <para>
/// <b>Why a seam.</b> <c>GET /recommendations/monetization</c> lives in the Recommendations module's
/// URL space, and Recommendations is composed before Financial, so it cannot reference it. Financial
/// owns the catalogue, the eligibility rules, subscriptions, purchases and placements; this answers
/// with facts only. Ranking and the sentence a member reads are the recommender's, so the two halves
/// cannot disagree about what was offered.
/// </para>
/// <para>
/// <b>Request-scoped.</b> Eligibility depends on the caller's access tier and verification, which
/// the implementation reads from the current request; it must only be asked about the caller.
/// </para>
/// </summary>
public interface IMonetizationOfferSource
{
    Task<MonetizationOfferSnapshot> ReadAsync(
        MonetizationOfferQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Which member, and which of their subjects a product could be bought for.</summary>
public sealed record MonetizationOfferQuery
{
    public required Guid UserId { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// The member's own subjects — listings, profiles, events — by Financial's subject type name
    /// (<c>MarketplaceListing</c>, <c>HousingListing</c>, <c>Job</c>, <c>Provider</c>,
    /// <c>Organization</c>, <c>Event</c>, <c>Guide</c>). Eligibility is evaluated per subject.
    /// </summary>
    public IReadOnlyList<MonetizationSubject> Subjects { get; init; } = [];
}

public sealed record MonetizationSubject(string SubjectType, Guid SubjectId);

/// <summary>Everything the recommender needs, as of one moment.</summary>
public sealed record MonetizationOfferSnapshot
{
    /// <summary>Products on sale now, each with the queried subjects it can be bought for.</summary>
    public required IReadOnlyList<MonetizationProductOffer> Products { get; init; }

    /// <summary>Active subscription plans offered in the member's country.</summary>
    public required IReadOnlyList<MonetizationPlanOffer> Plans { get; init; }

    /// <summary>The plan the member holds now (trial, active or in grace), or null.</summary>
    public string? CurrentPlanCode { get; init; }

    /// <summary>Promotions running or scheduled on the queried subjects, by anybody.</summary>
    public required IReadOnlyList<MonetizationHolding> Holdings { get; init; }

    /// <summary>The member's completed product purchases, newest first, bounded.</summary>
    public required IReadOnlyList<MonetizationPastPurchase> Purchases { get; init; }

    public static MonetizationOfferSnapshot Empty { get; } = new()
    {
        Products = [],
        Plans = [],
        Holdings = [],
        Purchases = []
    };
}

public sealed record MonetizationProductOffer
{
    public required Guid ProductId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary><c>MonetizationProductCategory</c> name.</summary>
    public required string Category { get; init; }

    /// <summary><c>MonetizationSubjectType</c> name.</summary>
    public required string SubjectType { get; init; }

    /// <summary>The list price in minor units, when the catalogue states one. Money is never a decimal here.</summary>
    public long? PriceMinorUnits { get; init; }

    /// <summary>Decimal places of <see cref="Currency"/>; 2 for GBP, 0 for XOF.</summary>
    public int PriceExponent { get; init; } = 2;

    public string? Currency { get; init; }

    public int? DurationDays { get; init; }

    public int SortOrder { get; init; }

    /// <summary>The queried subjects the eligibility rules allow this product to be bought for.</summary>
    public required IReadOnlyList<Guid> EligibleSubjectIds { get; init; }

    /// <summary>For a <c>Member</c>-subject product: whether the member may buy it for themselves.</summary>
    public bool IsEligibleForMember { get; init; }
}

public sealed record MonetizationPlanOffer
{
    public required Guid PlanId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary><c>SubscriptionTier</c> name.</summary>
    public required string Tier { get; init; }

    /// <summary><c>BillingInterval</c> name.</summary>
    public required string Interval { get; init; }

    /// <summary>The price per interval in minor units.</summary>
    public required long PriceMinorUnits { get; init; }

    public int PriceExponent { get; init; } = 2;

    public required string Currency { get; init; }

    /// <summary><c>FinancialAccountType</c> name: <c>Personal</c>, <c>Provider</c>, <c>Organization</c>…</summary>
    public required string AudienceType { get; init; }

    public int SortOrder { get; init; }

    public int TrialDays { get; init; }

    public bool IsFree { get; init; }

    /// <summary>Entitlement key to value; a flag entitlement has a null value.</summary>
    public required IReadOnlyDictionary<string, string?> Entitlements { get; init; }
}

/// <summary>A promotion running or scheduled on a subject.</summary>
public sealed record MonetizationHolding(
    string ProductCode,
    string SubjectType,
    Guid SubjectId,
    string Status,
    DateTimeOffset? EndsAt);

/// <summary>A completed purchase.</summary>
public sealed record MonetizationPastPurchase(
    string ProductCode,
    string Category,
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset PurchasedAt);

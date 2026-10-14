namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>
/// The area around a point: what is within <see cref="RadiusKm"/> of a place the member picked, or
/// of where they are, in any country.
/// <para>
/// <b>Phase 2A of retiring the place catalogue (owner's decision, 14 Oct 2026).</b> A neighbourhood
/// page used to describe a named catalogue node, and only the handful of places the catalogue held
/// had one. This describes a circle, so every place on earth has one. Everything on it is computed
/// from rows' own coordinates: listing, event and post counts within the circle, the median asking
/// rent within it against the country's, and safety as sourced counts.
/// </para>
/// <para>
/// <b>The name is the member's, never inferred.</b> <see cref="Name"/> is the Google place name the
/// client sent back unchanged, or null; <see cref="DisplayName"/> is that name or "Within 2 km of
/// you". The platform never names a circle from what it thinks is there.
/// </para>
/// </summary>
public sealed record AreaReportResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>The radius described, after the default and the cap were applied.</summary>
    public required double RadiusKm { get; init; }

    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    /// <summary>The place name the client sent — the Google place the member chose. Null when none was.</summary>
    public string? Name { get; init; }

    /// <summary><see cref="Name"/>, or "Within 2 km of you" when there is none. What a header renders.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Per-vertical counts within the circle: <c>housing</c>, <c>marketplace</c>, <c>jobs</c>,
    /// <c>events</c>, <c>updates</c>. A vertical that could not be asked is an absent key — and is
    /// named in <see cref="UnavailableResourceTypes"/> — never a zero.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ResourceCounts { get; init; }

    /// <summary>Counts that stopped at their reading cap: the true figure is at least the one given.</summary>
    public IReadOnlyList<string> LowerBoundResourceTypes { get; init; } = [];

    /// <summary>Verticals that failed or timed out, so their count is unknown rather than zero.</summary>
    public IReadOnlyList<string> UnavailableResourceTypes { get; init; } = [];

    /// <summary>
    /// Null, always, today: a blend needs scored dimensions, and a circle has none. Kept so the
    /// neighbourhood score screen's shape carries over.
    /// </summary>
    public SourcedValueResponse<int>? Overall { get; init; }

    /// <summary>Scored dimensions. Empty: see <see cref="UnavailableDimensions"/> for why each is not scored.</summary>
    public IReadOnlyList<ScoreDimensionResponse> Dimensions { get; init; } = [];

    /// <summary>Every dimension, with the reason the circle has no score for it.</summary>
    public required IReadOnlyList<UnavailableDimensionResponse> UnavailableDimensions { get; init; }

    public required AreaCostOfLivingResponse CostOfLiving { get; init; }

    public required AreaSafetyResponse Safety { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>
/// The rent within a circle against the country's, or why there is no figure.
/// <para>
/// The same rules as a neighbourhood's: the circle needs at least
/// <c>MinimumRentListingsInArea</c> homes to rent listed and the country
/// <c>MinimumRentListingsInCountry</c>, or no index is published and
/// <see cref="UnavailableReason"/> says <c>InsufficientSample</c> — "not enough data", never a guess.
/// </para>
/// </summary>
public sealed record AreaCostOfLivingResponse
{
    /// <summary>True when the rent index is published.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary><c>InsufficientSample</c> when there are too few homes listed. Null when available.</summary>
    public string? UnavailableReason { get; init; }

    /// <summary>Homes to rent within the circle behind the median.</summary>
    public required int SampleSize { get; init; }

    /// <summary>Homes to rent in the whole country behind the baseline.</summary>
    public required int CountrySampleSize { get; init; }

    /// <summary>Null, always: housing is the only measured category, and it is not "overall".</summary>
    public int? OverallIndex { get; init; }

    public string? BaselineName { get; init; }

    public string? Currency { get; init; }

    /// <summary><c>housing</c> when available; empty otherwise. An absent category is not measured, not zero.</summary>
    public IReadOnlyList<CostOfLivingCategoryResponse> Categories { get; init; } = [];

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>
/// Safety around a point: sourced counts, never a score (decided 16 Sep 2026).
/// </summary>
public sealed record AreaSafetyResponse
{
    /// <summary>False, always: no source measures safety, only incidents.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Null, always.</summary>
    public int? Score { get; init; }

    /// <summary>Null, always.</summary>
    public string? Band { get; init; }

    /// <summary><c>NoDataSource</c>.</summary>
    public required string UnavailableReason { get; init; }

    /// <summary>
    /// One entry per source: <c>community-reports</c> within the circle, and
    /// <c>police-recorded-crime</c> within one mile of the point where data.police.uk covers the
    /// country — or, everywhere else, an entry with <c>isAvailable: false</c> and
    /// <c>unavailableReason: NotPublished</c>, which is "not available here", not zero.
    /// </summary>
    public required IReadOnlyList<SafetyCountResponse> Counts { get; init; }

    public required IReadOnlyList<string> Limitations { get; init; }
}

/// <summary>Two or more areas around points, composed identically, in the caller's order.</summary>
public sealed record AreaComparisonResponse
{
    public required double RadiusKm { get; init; }

    public required IReadOnlyList<AreaReportResponse> Areas { get; init; }
}

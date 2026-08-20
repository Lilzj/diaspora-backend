namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>Change against a previous period, where a trend can actually be computed.</summary>
public sealed record ScoreTrendResponse
{
    /// <summary><c>up</c>, <c>down</c> or <c>flat</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Absolute points of change over <see cref="PeriodDays"/>.</summary>
    public required int Delta { get; init; }

    public required int PeriodDays { get; init; }
}

/// <summary>
/// One dimension of a neighbourhood score, with everything §26 requires around it.
/// <para>
/// <b>A dimension only appears here when something real produced it.</b> There is no placeholder
/// row, no zero standing in for an absence and no estimate. Anything the platform cannot answer
/// appears in <see cref="NeighborhoodScoreResponse.UnavailableDimensions"/> with the reason it
/// cannot, which is a fact the client has been inferring and can now read.
/// </para>
/// </summary>
public sealed record ScoreDimensionResponse
{
    /// <summary>One of the nine ids the client's union type accepts, kebab-case.</summary>
    public required string Id { get; init; }

    public required string Label { get; init; }

    /// <summary>0–100.</summary>
    public required int Score { get; init; }

    /// <summary>
    /// The ordinal band behind the score, where the dimension is an assessment rather than a rank.
    /// <para>
    /// Published beside the number because it, not the number, is what a moderator actually
    /// recorded. A client that prefers to render "High" over "70" is rendering the more accurate
    /// of the two.
    /// </para>
    /// </summary>
    public string? Band { get; init; }

    /// <summary>Why this number. Shown in the breakdown, and never invented by the client.</summary>
    public string? Explanation { get; init; }

    /// <summary>
    /// Absent on every dimension today, and the field is here rather than omitted so that adding a
    /// history does not change this contract. Nothing on this platform stores what a neighbourhood
    /// scored last quarter, and a direction derived from one reading is an impression.
    /// </summary>
    public ScoreTrendResponse? Trend { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>A dimension the platform is not publishing, and why.</summary>
public sealed record UnavailableDimensionResponse
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    /// <summary>
    /// <c>NoDataSource</c>, <c>NotAssessed</c>, <c>InsufficientPopulation</c> or
    /// <c>SourceUnavailable</c>. A closed set a client may branch on.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>A sentence a member can read, not an operator's diagnostic.</summary>
    public required string Explanation { get; init; }
}

/// <summary>
/// A value with its provenance attached — the wire form of the client's <c>Sourced&lt;T&gt;</c>.
/// </summary>
public sealed record SourcedValueResponse<T>
{
    public required T Value { get; init; }

    /// <summary><c>backend</c>. This module never emits <c>simulated</c>.</summary>
    public required string Source { get; init; }
}

/// <summary>
/// The full score breakdown for a neighbourhood.
/// <para>
/// <b>The overall figure is computed only from dimensions that are in the response.</b> The
/// frontend types <c>overall</c> as <c>Sourced&lt;number&gt;</c> and marks it <em>simulated</em>
/// today, for a reason its register states exactly: "a blend containing an invented input is not a
/// backend value". Nothing invented reaches this blend, so this one is marked <c>backend</c> — and
/// when no dimension has a source at all it is <c>null</c> rather than zero, because a
/// neighbourhood nobody has assessed must not read as a terrible one.
/// </para>
/// </summary>
public sealed record NeighborhoodScoreResponse
{
    /// <summary>
    /// The client's route parameter: B4's slug.
    /// <para>
    /// The register asks for "a stable neighbourhood identifier scheme; ids are currently derived
    /// by slugifying a name, which breaks on rename and collides across cities."
    /// <see cref="AreaId"/> beside this is that identifier — it is B4's primary key, it survives a
    /// rename, and it is unique across every city on the platform. The slug stays as
    /// <see cref="NeighborhoodId"/> so existing links keep resolving.
    /// </para>
    /// </summary>
    public required string NeighborhoodId { get; init; }

    /// <summary>B4's area identifier. The stable key, and the one to migrate links to.</summary>
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    public string? CityName { get; init; }

    public required string CountryCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>0–100, blended from <see cref="Dimensions"/> alone. Null when none has a source.</summary>
    public SourcedValueResponse<int>? Overall { get; init; }

    public required IReadOnlyList<ScoreDimensionResponse> Dimensions { get; init; }

    /// <summary>
    /// Every dimension not in <see cref="Dimensions"/>, with the reason.
    /// <para>
    /// An addition to the shape the client declared, and the most useful field on the record: it
    /// is what lets a screen distinguish "nobody has rated this place yet" from "this platform has
    /// no safety data at all", which currently look identical and are both filled with an estimate.
    /// </para>
    /// </summary>
    public required IReadOnlyList<UnavailableDimensionResponse> UnavailableDimensions { get; init; }

    /// <summary>
    /// The real per-vertical counts for this place, exactly as B4's counter registry reports them.
    /// <para>
    /// Passed through unchanged rather than folded into dimensions, so a client that wants "14
    /// homes" instead of "housing: 62nd percentile" has the figure and does not have to invert an
    /// encoding to get it. A vertical that could not be reached is an absent key, never a zero.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, int> ResourceCounts { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }

    /// <summary>Adjacent neighbourhoods, for the comparison surface. B4's siblings of this node.</summary>
    public IReadOnlyList<string> NearbyNeighborhoodIds { get; init; } = [];
}

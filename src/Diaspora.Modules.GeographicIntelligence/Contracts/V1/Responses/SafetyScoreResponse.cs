namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>
/// The safety surface, answering honestly.
/// <para>
/// <b>There is no safety score on this platform, and this response is how a client finds that out
/// instead of assuming it.</b> The column exists on B4's area, nothing writes to it, B11's nine
/// assessed bands deliberately exclude safety, and no data source is contracted. The frontend's
/// register rated safety provenance its highest-consequence item on the premise that
/// <c>stats.safetyScore</c> was "real and rendered as a real value"; it is null on every row and
/// always has been.
/// </para>
/// <para>
/// <b>Why serve the route at all, rather than leave it absent.</b> Because absence is what the
/// client already has, and absence is indistinguishable from an outage. A route that returns
/// <see cref="IsAvailable"/> false with a named reason and the platform's own limitations text
/// lets the safety screen stop simulating provenance and start quoting the server — which is what
/// §26 asks for and what the frontend cannot do for itself.
/// </para>
/// <para>
/// A safety score materially affects where somebody chooses to live, and this community includes
/// people with the least margin for a bad move. Publishing <c>computedAt</c>, <c>sourceName</c>,
/// <c>methodology</c> and <c>sampleSize</c> around a number nobody computes would be the strongest
/// possible version of exactly the fiction the frontend was right to refuse.
/// </para>
/// </summary>
public sealed record SafetyScoreResponse
{
    public required string NeighborhoodId { get; init; }

    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    /// <summary>False on every deployment until a safety data source is contracted.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>0–100, higher is safer. Null while <see cref="IsAvailable"/> is false.</summary>
    public int? Score { get; init; }

    /// <summary><c>low</c>, <c>moderate</c>, <c>good</c> or <c>high</c>. Null with the score.</summary>
    public string? Band { get; init; }

    /// <summary>Sub-dimensions, once a source publishes any. Empty today.</summary>
    public IReadOnlyList<ScoreDimensionResponse> Breakdown { get; init; } = [];

    public ScoreTrendResponse? Trend { get; init; }

    /// <summary>
    /// Why there is no score, as a code a client may branch on: <c>NoDataSource</c>.
    /// </summary>
    public string? UnavailableReason { get; init; }

    /// <summary>
    /// What the platform can say about safety in place of a score.
    /// <para>
    /// Server-owned text so the client's mandatory limitations block has something authoritative
    /// to render. It should keep rendering the block either way — this only means it no longer has
    /// to write the words itself.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> Limitations { get; init; }

    /// <summary>
    /// Sourced incident counts, one entry per source, never blended (17 Sep 2026).
    /// <para>
    /// The user decided the platform publishes counts from named sources instead of a score:
    /// corroborated community safety reports everywhere, and police-recorded street crime where an
    /// official open source covers the country. Each entry states its source, licence, period, area
    /// and method, and an entry whose source could not be read says so rather than reading zero.
    /// </para>
    /// </summary>
    public IReadOnlyList<SafetyCountResponse> Counts { get; init; } = [];
}

/// <summary>One source's count of incidents near a place, with everything needed to read it.</summary>
public sealed record SafetyCountResponse
{
    /// <summary><c>community-reports</c> or <c>police-recorded-crime</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Who counted: "Diaspora community reports", "data.police.uk".</summary>
    public required string Source { get; init; }

    /// <summary>
    /// False when the source could not be read, holds nothing for this place, or does not exist.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Why not: <c>SourceUnavailable</c>, <c>NoCoverage</c>, <c>Disabled</c>, <c>NoCentre</c>, or
    /// <c>NotPublished</c> — nobody publishes this figure for the country at all.
    /// </summary>
    public string? UnavailableReason { get; init; }

    /// <summary>The start of the period counted.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>The end of the period counted.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>The ground covered, in words: "Within one mile of Peckham's centre".</summary>
    public required string Area { get; init; }

    /// <summary>How the figure was produced, in one or two sentences.</summary>
    public required string Method { get; init; }

    /// <summary>The licence the counts are published under, where the source has one: <c>OGL-UK-3.0</c>.</summary>
    public string? Licence { get; init; }

    /// <summary>The credit the licence requires, verbatim, to show with the figure.</summary>
    public string? Attribution { get; init; }

    public int? Total { get; init; }

    /// <summary>Counts per category, largest first.</summary>
    public IReadOnlyList<SafetyCountCategoryResponse> Categories { get; init; } = [];
}

/// <summary>One category of a sourced safety count.</summary>
public sealed record SafetyCountCategoryResponse
{
    /// <summary>The source's own identifier: <c>anti-social-behaviour</c>, <c>Police</c>.</summary>
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required int Count { get; init; }
}

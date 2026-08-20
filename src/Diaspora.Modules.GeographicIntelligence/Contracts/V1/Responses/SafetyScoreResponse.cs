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
}

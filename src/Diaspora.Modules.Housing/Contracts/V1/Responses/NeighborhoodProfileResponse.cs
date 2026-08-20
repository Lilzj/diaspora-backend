namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A neighbourhood, as Housing describes it.
/// <para>
/// <b>The geographic half is B4's and the descriptive half is Housing's</b>, and this record is
/// where the two are joined for a client. <see cref="AreaId"/> is B4's identifier: a client wanting
/// the boundary, the parent city, the coordinates or the resource counts asks Location with it. A
/// client wanting to know what living there is like has already got that here.
/// </para>
/// <para>
/// <see cref="Completeness"/> is rendered as a prompt to contribute, never as a quality score. A
/// thin profile is a place nobody has written about yet, not a bad place to live.
/// </para>
/// </summary>
public sealed record NeighborhoodProfileResponse
{
    // ── The place, which Location owns ──────────────────────────────────────────────────────

    /// <summary>B4's area identifier. The key every other module already shares.</summary>
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    /// <summary>What the client's neighbourhood route addresses places by.</summary>
    public required string Slug { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? RegionName { get; init; }

    // ── The profile, which Housing owns ─────────────────────────────────────────────────────

    public string? Description { get; init; }

    public string? Overview { get; init; }

    public string? SafetySummary { get; init; }

    public string? TransportSummary { get; init; }

    public string? CostOfLivingSummary { get; init; }

    /// <summary>Who lives there. The paragraph a diaspora member reads first.</summary>
    public string? CommunitySummary { get; init; }

    public string? ClimateNotes { get; init; }

    public required NeighborhoodLivingIndexResponse LivingIndex { get; init; }

    /// <summary>Languages you will hear, most prominent first.</summary>
    public IReadOnlyList<NeighborhoodLanguageResponse> PopularLanguages { get; init; } = [];

    /// <summary>Published community insights, most attested first.</summary>
    public IReadOnlyList<NeighborhoodInsightResponse> Insights { get; init; } = [];

    public required NeighborhoodStatisticsResponse Statistics { get; init; }

    // ── Editorial state ─────────────────────────────────────────────────────────────────────

    /// <summary>Whether an approver has signed the current profile off.</summary>
    public required bool IsApproved { get; init; }

    /// <summary>Nought to one. A prompt to contribute, not a quality score.</summary>
    public required double Completeness { get; init; }

    /// <summary>When it was last reviewed. Prose about a place goes stale.</summary>
    public DateTimeOffset? LastReviewedAt { get; init; }

    /// <summary>
    /// Whether the profile has gone past the deployment's review interval. Rendered to staff as a
    /// queue and to members as nothing at all.
    /// </summary>
    public required bool NeedsReview { get; init; }

    /// <summary>The optimistic-concurrency token an editor sends back.</summary>
    public required int Revision { get; init; }
}

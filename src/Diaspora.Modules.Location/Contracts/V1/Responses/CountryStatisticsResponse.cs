namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The counts a country page renders.
/// <para>
/// Field-for-field the frontend's <c>CountryStatistics</c>, minus <c>trendingGuideIds</c> and
/// <c>topContributorIds</c> — those are content rankings owned by whichever module owns
/// guides and contributors, and Location serving them would be Location deciding what is
/// trending. The counts themselves come from <c>ILocationResourceCounter</c> contributions,
/// so Location aggregates them without knowing what a guide is.
/// </para>
/// <para>
/// A vertical that has registered no counter contributes no key. A client must therefore read
/// a missing key as "unknown", not as zero — which is why the counts are a dictionary rather
/// than a fixed set of properties that would all have to be present and would all be lies.
/// </para>
/// </summary>
public sealed record CountryStatisticsResponse
{
    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    public required string Flag { get; init; }

    public string? LandmarkImageUrl { get; init; }

    /// <summary>Keyed by contributing resource type: <c>providers</c>, <c>guides</c>, and so on.</summary>
    public required IReadOnlyDictionary<string, int> Counts { get; init; }

    public required DateTimeOffset LastUpdatedAt { get; init; }
}

namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// The counts a results page renders beside its filters.
/// <para>
/// Every group may legitimately be empty: an engine that cannot facet cheaply says so rather than
/// faceting expensively, and a filter list without counts is still a filter list.
/// </para>
/// </summary>
public sealed record ListingSearchFacetsResponse
{
    public required IReadOnlyDictionary<string, int> Categories { get; init; }

    public required IReadOnlyDictionary<string, int> Countries { get; init; }

    public required IReadOnlyDictionary<string, int> Cities { get; init; }

    public required IReadOnlyDictionary<string, int> Conditions { get; init; }

    public required IReadOnlyDictionary<string, int> ListingTypes { get; init; }

    public required IReadOnlyDictionary<string, int> Brands { get; init; }

    /// <summary>Bands keyed by a label the engine chose — <c>0-50</c>, <c>50-200</c>.</summary>
    public required IReadOnlyDictionary<string, int> PriceBands { get; init; }
}

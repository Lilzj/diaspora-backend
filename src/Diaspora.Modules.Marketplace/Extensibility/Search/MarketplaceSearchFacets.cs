namespace Diaspora.Modules.Marketplace.Extensibility.Search;

/// <summary>
/// The counts a results page renders beside its filters.
/// <para>
/// Every group is a plain string-to-count map, so an engine contributes whatever it can compute
/// and the client renders whatever arrives. An empty set is a valid answer — an engine that cannot
/// facet cheaply should say so rather than facet expensively, and a filter list without counts is
/// still a filter list.
/// </para>
/// </summary>
public sealed record MarketplaceSearchFacets
{
    public IReadOnlyDictionary<string, int> Categories { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Countries { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Cities { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Conditions { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ListingTypes { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Brands { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Price bands, keyed by a label the engine chose — <c>0-50</c>, <c>50-200</c>. Bands rather
    /// than a histogram, because the client renders checkboxes and because a band label is
    /// meaningful only alongside the currency the search was scoped to.
    /// </summary>
    public IReadOnlyDictionary<string, int> PriceBands { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Nothing computed. What an engine says when faceting would cost more than it is worth.</summary>
    public static MarketplaceSearchFacets Empty => new();
}

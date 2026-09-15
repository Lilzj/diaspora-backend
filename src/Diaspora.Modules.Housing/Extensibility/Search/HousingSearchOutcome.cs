using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Extensibility.Search;

/// <summary>
/// What an engine hands back.
/// <para>
/// <b>It returns the aggregates rather than a projection</b>, and that is the decision that keeps
/// the port honest: an external engine returns identifiers, the adapter re-reads the listings from
/// the write model, and the caller therefore renders a card from the same source of truth
/// regardless of which engine answered. An engine that returned its own denormalised copy would
/// let a search result and a detail page disagree about the rent.
/// </para>
/// </summary>
public sealed record HousingSearchOutcome
{
    /// <summary>The listings, in the engine's order. Never re-sorted by the caller.</summary>
    public required IReadOnlyList<HousingListing> Items { get; init; }

    /// <summary>The continuation token for the next page, or null when there is none.</summary>
    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>
    /// The total, where the engine can produce one cheaply. Null rather than zero when it cannot —
    /// an engine that returned zero for "I do not count" would make a page of results claim to be
    /// empty.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>Counts beside the filters. Null when the caller did not ask for them.</summary>
    public HousingSearchFacets? Facets { get; init; }

    /// <summary>
    /// Which engine answered. Surfaced in the response so an operator can tell whether the external
    /// engine or the EF fallback served a request without reading a log.
    /// </summary>
    public string? EngineName { get; init; }

    /// <summary>
    /// The order the items are actually in, when that is not the one asked for — "nearest first"
    /// with no point to measure from, or over more candidates than the engine will measure. Null
    /// when the requested order was honoured.
    /// </summary>
    public Domain.Enums.HousingSortOrder? AppliedSort { get; init; }
}

using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Extensibility.Search;

/// <summary>
/// One search, expressed in the module's own vocabulary.
/// <para>
/// <b>It carries the domain filter rather than a query string</b>, so an engine decides for itself
/// whether "two bedrooms under 900 a month with pets allowed in Peckham" becomes a bool query, a
/// filter chain or a SQL predicate. A port that took a query string would have frozen one engine's
/// dialect into the domain, which is the mistake this seam exists to avoid.
/// </para>
/// </summary>
public sealed record HousingSearchRequest
{
    /// <summary>
    /// Everything to narrow by, including the audience fields the authorisation service filled in.
    /// <b>An engine must honour those</b>: a search index that returned drafts or hidden listings
    /// would defeat every visibility rule the module enforces elsewhere.
    /// </summary>
    public required HousingListingQueryFilter Filter { get; init; }

    /// <summary>The opaque continuation token, or null for the first page.</summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Whether the caller wants facet counts. Off for a nearby panel, on for the search screen —
    /// computing them is the expensive half of the query and most callers do not render them.
    /// </summary>
    public bool IncludeFacets { get; init; }

    /// <summary>
    /// A resolved point, when the caller supplied coordinates. Carried separately from the filter
    /// because the shipped EF engine cannot use it and a geospatial engine must.
    /// </summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The radius in kilometres, resolved by B4. Null when the search is not point-based.</summary>
    public double? RadiusKm { get; init; }
}

namespace Diaspora.Modules.Housing.Extensibility.Catalog;

/// <summary>
/// One amenity a contributor supplies.
/// <para>
/// <see cref="IsAccessibilityFeature"/> is called out rather than left to the group, and
/// deliberately: step-free access is not a convenience to be listed beside a swimming pool. For
/// the members who need it, it is the filter that decides whether a property is habitable at all,
/// and it must work regardless of how a deployment chose to group things.
/// </para>
/// </summary>
public sealed record HousingAmenitySeed
{
    /// <summary>The stable key listings will store. Normalised on the way in.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The presentation group: utilities, safety, connectivity, outdoor space.</summary>
    public string? Group { get; init; }

    /// <summary>An icon key the client resolves. A name, never a URL.</summary>
    public string? IconKey { get; init; }

    /// <summary>Whether this is an accessibility feature. See the class summary.</summary>
    public bool IsAccessibilityFeature { get; init; }

    /// <summary>
    /// Whether the search facets offer it. Not every amenity deserves a checkbox: a facet list of
    /// forty is a facet list nobody reads.
    /// </summary>
    public bool IsFilterable { get; init; } = true;

    public int Order { get; init; }
}

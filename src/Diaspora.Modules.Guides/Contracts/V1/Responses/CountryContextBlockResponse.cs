namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One contextual band on a country dashboard — "142 jobs", "38 homes".
/// <para>
/// A count and a route. The route points at the module that owns the content, which is what keeps
/// the dashboard a directory: the Knowledge Directory says how much there is and where to go, and
/// the owning vertical answers what it is, under its own rules.
/// </para>
/// </summary>
public sealed record CountryContextBlockResponse
{
    /// <summary>The counter kind — <c>jobs</c>, <c>housing</c>. Clients switch on it for an icon.</summary>
    public required string Kind { get; init; }

    public required string Label { get; init; }

    public required int Count { get; init; }

    /// <summary>A relative API path on the owning module, or null when it named none.</summary>
    public string? Route { get; init; }
}

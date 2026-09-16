using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// Everything a vertical knows about where one of its rows is, in the terms it holds it.
/// <para>
/// Deliberately a bag of optionals, because the four verticals hold wholly different things. A
/// property and a provider carry coordinates; a marketplace listing and a job posting carry a
/// country and the names a member typed and nothing else. One seam takes whichever subset
/// exists rather than four verticals each growing their own gazetteer.
/// </para>
/// </summary>
public sealed record AreaFilingRequest
{
    /// <summary>ISO 3166-1 alpha-2. Narrows every lookup; a wrong country files nothing.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// A catalogue node the row already names — a marketplace listing picked from the hierarchy,
    /// say, whose path was never stored. When it exists it is the answer, because the member chose
    /// it; nothing is inferred over the top of a choice.
    /// </summary>
    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The member's own word for the neighbourhood — "Peckham", "Rose Hill".</summary>
    public string? NeighbourhoodName { get; init; }

    public string? CityName { get; init; }

    public string? RegionName { get; init; }
}

/// <summary>
/// Where the catalogue says that is: the node, and what filing under it costs in precision.
/// </summary>
/// <param name="AreaId">The catalogue node's identifier, stored on the row.</param>
/// <param name="Path">
/// The materialised path. This is the field that matters: every resource counter on the
/// platform, and the map's cluster endpoint, aggregate by prefix-matching it.
/// </param>
/// <param name="Name">The node's name, for the label a card renders.</param>
/// <param name="CountryCode">The catalogue's country for the node, not the caller's claim.</param>
/// <param name="Level">How specific the answer is. A city is an honest answer; a country is not.</param>
public sealed record AreaPlacement(
    Guid AreaId,
    string Path,
    string Name,
    string CountryCode,
    AdministrativeLevel Level);

/// <summary>
/// Files a row under the catalogue node it belongs to.
/// <para>
/// <b>Until 16 Sep 2026 nothing on this platform was filed under an area at all.</b> Every
/// vertical stored an <c>AreaId</c> and an <c>AreaPath</c>, every resource counter aggregated by
/// prefix-matching that path, and no create screen ever sent one — so a town search by path
/// matched nothing, every neighbourhood's resource count read zero, and
/// <c>GET /location/map/clusters</c> aggregated a catalogue of noughts. The endpoint was not
/// broken; it was correctly counting nothing.
/// </para>
/// <para>
/// <b>Distinct from <see cref="ILocationDirectory.FindNearestAreaAsync"/>, which answers a
/// different question.</b> That one searches 150 km and returns whatever is closest, which is
/// right for "which town is this pin near" and wrong for filing: it would put a house in open
/// country into a neighbourhood nobody would say it was in. This one refuses to guess, and a row
/// it cannot place stays unplaced.
/// </para>
/// </summary>
public interface IAreaLocator
{
    /// <summary>
    /// The most specific node the request's own geography lands in, or null.
    /// <para>
    /// An area the row already names wins outright. Otherwise the names the member typed decide
    /// which place — neighbourhood, then city, then region — and coordinates only refine within it:
    /// a pin can file a flat in one of Quatre Bornes' neighbourhoods, never in Rose Hill next door.
    /// With no name the catalogue knows, the coordinates decide alone. Nothing falls back to the
    /// country node: filing every listing in Mauritius under Mauritius would make each country one
    /// enormous cluster and tell a member reading the map nothing they did not already know.
    /// </para>
    /// </summary>
    Task<AreaPlacement?> LocateAsync(
        AreaFilingRequest request, CancellationToken cancellationToken = default);
}

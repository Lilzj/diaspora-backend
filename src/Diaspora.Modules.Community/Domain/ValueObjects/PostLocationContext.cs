using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// Where a post is about.
/// <para>
/// <b>Every field here is a copy of something Location owns, and that is deliberate.</b> A feed
/// query that filters on country, orders by distance and renders a neighbourhood name cannot
/// join across a module boundary on every row — that is not a purity concern, it is the
/// difference between a feed that answers in ten milliseconds and one that answers in two
/// hundred.
/// </para>
/// <para>
/// The rule that keeps this from becoming duplicated logic: <b>B6 stores geographic facts and
/// performs no geographic reasoning.</b> Resolving loose client parameters into a bounded scope
/// is Location's <c>IGeographicScopeResolver</c>; computing a distance is Location's
/// <c>ILocationDirectory</c>; deciding what counts as nearby is Location's. B6 holds the
/// coordinates and asks.
/// </para>
/// <para>
/// The one arithmetic exception is the bounding-box prefilter the nearby feed uses to narrow
/// candidate rows before Location is asked for real distances. It is a filter, not an answer,
/// and it is documented as such where it lives.
/// </para>
/// </summary>
public sealed class PostLocationContext : ValueObject
{
    private PostLocationContext(
        string? countryCode,
        Guid? areaId,
        string? areaName,
        string? cityName,
        double? latitude,
        double? longitude,
        string? hierarchyPath,
        bool pointIsExact)
    {
        CountryCode = countryCode;
        AreaId = areaId;
        AreaName = areaName;
        CityName = cityName;
        Latitude = latitude;
        Longitude = longitude;
        HierarchyPath = hierarchyPath;
        PointIsExact = pointIsExact && latitude is not null && longitude is not null;
    }

    private PostLocationContext() { }

    /// <summary>ISO 3166-1 alpha-2, upper-cased. The coarsest and most-queried filter.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>The Location administrative area this post sits in, if one was resolved.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>The client renders this as both <c>areaName</c> and <c>neighborhood</c>.</summary>
    public string? AreaName { get; private set; }

    public string? CityName { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>
    /// Location's materialised hierarchy path, copied so a neighbourhood feed can prefix-match
    /// without a join. A plain string for the reason B4 records: EF cannot translate
    /// <c>StartsWith</c> against a member of a value-converted type, and every hierarchy query
    /// is a prefix match.
    /// </summary>
    public string? HierarchyPath { get; private set; }

    /// <summary>
    /// Whether the coordinates are where the author put them, rather than the centre of the place
    /// they named.
    /// <para>
    /// <b>Most posts' coordinates are a centroid.</b> The app's alert composer sends a place name and
    /// no pin, and the post is placed at the centre of the town Location resolves — which keeps it on
    /// the map, and puts every London alert on the same spot. Found on 16 Sep 2026, when an affected
    /// area around "London" was accepted: a radius drawn around a centroid is a precise-looking claim
    /// about somewhere nobody chose. A map uses this to mark an alert as approximate, and an affected
    /// area needs it to be true.
    /// </para>
    /// </summary>
    public bool PointIsExact { get; private set; }

    public bool HasCoordinates => Latitude is not null && Longitude is not null;

    public bool IsEmpty =>
        CountryCode is null && AreaId is null && CityName is null && !HasCoordinates;

    public static PostLocationContext None =>
        new(null, null, null, null, null, null, null, pointIsExact: false);

    public static PostLocationContext Create(
        string? countryCode = null,
        Guid? areaId = null,
        string? areaName = null,
        string? cityName = null,
        double? latitude = null,
        double? longitude = null,
        string? hierarchyPath = null,
        bool pointIsExact = false) =>
        new(
            string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant(),
            areaId,
            string.IsNullOrWhiteSpace(areaName) ? null : areaName.Trim(),
            string.IsNullOrWhiteSpace(cityName) ? null : cityName.Trim(),
            latitude,
            longitude,
            string.IsNullOrWhiteSpace(hierarchyPath) ? null : hierarchyPath.Trim(),
            pointIsExact);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return AreaId;
        yield return AreaName;
        yield return CityName;
        yield return Latitude;
        yield return Longitude;
        yield return HierarchyPath;
        yield return PointIsExact;
    }
}

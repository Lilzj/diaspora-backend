using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// The raw, unvalidated geography a client sent, in exactly the shape the frontend sends it.
/// <para>
/// Every field is nullable and every field is optional, because that is the truth of the
/// wire: <c>discover.tsx</c> sends city, latitude, longitude and radius together; the housing
/// detail screen sends city and coordinates but no radius; community life sends only a city;
/// two call sites spell the parameter <c>city</c> instead of <c>cityName</c>. All of it is
/// accepted here and normalised once.
/// </para>
/// </summary>
public sealed record GeographicScopeRequest
{
    /// <summary>The frontend's <c>cityName</c>, and its <c>city</c> — both bind here.</summary>
    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>
    /// The frontend's <c>radiusKm</c> — and its <c>distanceRadius</c>, which is the same
    /// parameter under a second name on the marketplace filter. Sentinels accepted.
    /// </summary>
    public double? RadiusKm { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// A map viewport, exactly as the client sends it: <c>north,south,east,west</c>.
    /// <para>
    /// <b>A string, and unparsed, for the same reason every other field here is a raw
    /// primitive.</b> A map is a rectangle and every certified search route took a centre and a
    /// radius, so six discovery routes now accept a box — and not one of them parses it,
    /// validates it, or has to know that <c>west &gt; east</c> means the box crosses ±180°
    /// rather than that it spans the planet.
    /// </para>
    /// <para>
    /// Wins over <see cref="Latitude"/>/<see cref="Longitude"/> when both are present: a
    /// viewport is the more explicit statement, and a map that sends its centre alongside its
    /// box means the box.
    /// </para>
    /// </summary>
    public string? Bounds { get; init; }

    /// <summary>A catalogue node id, when the client already has one.</summary>
    public AreaId? AreaId { get; init; }

    /// <summary>
    /// The member whose saved profile fills the gaps.
    /// <para>
    /// When set and the request carries no geography of its own, the scope resolves from the
    /// member's active location — so a vertical gets "near me" behaviour without implementing
    /// it, and travel mode is honoured everywhere without any vertical knowing travel mode
    /// exists.
    /// </para>
    /// </summary>
    public UserId? UserId { get; init; }

    /// <summary>Whether an unresolvable city name should be an error or simply ignored.</summary>
    public bool StrictCityResolution { get; init; }
}

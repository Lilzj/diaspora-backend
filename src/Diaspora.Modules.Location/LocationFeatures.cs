namespace Diaspora.Modules.Location;

/// <summary>
/// The feature gates the Location module owns.
/// <para>
/// Gates tell a client what to show; the permissions decide what the server will do. The
/// pairing matters for discovery in particular: a member who cannot yet publish anything
/// still browses by neighbourhood from their first session, so almost every gate here opens
/// at <c>Limited</c>.
/// </para>
/// </summary>
public static class LocationFeatures
{
    public const string Discovery = "location.discovery";
    public const string Nearby = "location.nearby";
    public const string Map = "location.map";
    public const string NeighborhoodHub = "location.neighborhood-hub";
    public const string SavedLocations = "location.saved-locations";
    public const string TravelMode = "location.travel-mode";
    public const string CountryExperience = "location.country-experience";
    public const string CountryPage = "location.country-page";
}

namespace Diaspora.Modules.Location;

/// <summary>
/// Stable, client-facing error codes for the module's problem documents.
/// <para>
/// Frozen alongside the API: a client branching on <c>location.area.not-found</c> must keep
/// working across releases, so these strings change only with a new API version.
/// </para>
/// </summary>
public static class LocationErrorCodes
{
    public const string NotAuthenticated = "location.not-authenticated";
    public const string CountryNotFound = "location.country.not-found";
    public const string CountryNotSupported = "location.country.not-supported";
    public const string AreaNotFound = "location.area.not-found";
    public const string AreaLevelMismatch = "location.area.level-mismatch";
    public const string NeighborhoodNotFound = "location.neighborhood.not-found";
    public const string CoordinatesInvalid = "location.coordinates.invalid";
    public const string RadiusInvalid = "location.radius.invalid";
    public const string BoundsInvalid = "location.bounds.invalid";
    public const string PolygonInvalid = "location.polygon.invalid";
    public const string ProfileNotFound = "location.profile.not-found";
    public const string SavedLocationNotFound = "location.saved-location.not-found";
    public const string SavedLocationLimitReached = "location.saved-location.limit-reached";
    public const string ExperienceNotFound = "location.experience.not-found";
    public const string ExperienceDuplicate = "location.experience.duplicate";
    public const string ExperienceOverlapping = "location.experience.overlapping";
    public const string ExperiencePeriodInvalid = "location.experience.period-invalid";
    public const string ExpertiseDuplicate = "location.expertise.duplicate";
    public const string ExpertiseLimitReached = "location.expertise.limit-reached";
    public const string GeocodingUnavailable = "location.geocoding.unavailable";
    public const string SpatialCapabilityUnsupported = "location.spatial.unsupported";

    /// <summary>21 Oct 2026. A presence report while "when the app is open" is switched off.</summary>
    public const string PresenceNotEnabled = "location.presence.not-enabled";
}

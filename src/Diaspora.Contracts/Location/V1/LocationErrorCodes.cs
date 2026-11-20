namespace Diaspora.Contracts.Location.V1;

/// <summary>
/// Stable, client-facing error codes for the module's problem documents.
/// <para>
/// Frozen alongside the API: a client branching on <c>location.area.not-found</c> must keep
/// working across releases, so these strings change only with a new API version.
/// </para>
/// <para>
/// <b>Published with B4's vocabulary rather than left in the module.</b> A type that other
/// modules are given has to be able to state its failures in terms they can also be given:
/// <c>CountryCode</c>, <c>GeoCoordinate</c> and the rest return <c>Result</c> failures
/// carrying these codes, and a leaf assembly cannot reach back into the module that owns
/// them. The strings are unchanged, and this file already described itself as frozen
/// alongside the API — which is what <c>V1</c> means. BARCH-001 / BSOLID-003.
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

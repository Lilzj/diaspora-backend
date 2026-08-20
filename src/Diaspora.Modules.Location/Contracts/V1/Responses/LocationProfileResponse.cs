namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The member's whole geographic context in one payload.
/// <para>
/// The server-side form of the frontend's Zustand location store, and shaped to replace it
/// field for field: <c>currentLocation</c>, <c>searchRadius</c>, <c>savedLocations</c>,
/// <c>recentLocations</c>, <c>travelMode</c>, <c>travelLocation</c> and
/// <c>permissionStatus</c> all appear. One fetch rehydrates the whole store, on any device.
/// </para>
/// </summary>
public sealed record LocationProfileResponse
{
    /// <summary>Where the member is when travel mode is off.</summary>
    public LocationReferenceResponse? CurrentLocation { get; init; }

    /// <summary>Where they are while travel mode is on.</summary>
    public LocationReferenceResponse? TravelLocation { get; init; }

    /// <summary>
    /// The one every query should use. The server-side equivalent of the client's
    /// <c>getActiveLocation</c> selector, resolved here so no consumer has to reimplement it.
    /// </summary>
    public LocationReferenceResponse? ActiveLocation { get; init; }

    public required bool TravelMode { get; init; }

    /// <summary><c>granted</c>, <c>denied</c> or <c>undetermined</c> — lower-case, matching
    /// the client's own union. Intentional deviation #10.</summary>
    public required string PermissionStatus { get; init; }

    public string? OriginCountryCode { get; init; }

    public string? HomeCountryCode { get; init; }

    public required GeographicPreferenceResponse Preferences { get; init; }

    public required IReadOnlyList<SavedLocationResponse> SavedLocations { get; init; }

    /// <summary>Most recent first, capped at five to match the client's own reducer.</summary>
    public required IReadOnlyList<LocationReferenceResponse> RecentLocations { get; init; }
}

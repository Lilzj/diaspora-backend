namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>
/// Changes how geography behaves for the member.
/// <para>
/// Every field is nullable and null means "leave alone", so a client changing only the radius
/// need not round-trip the rest. A partial update on a PUT is a small liberty taken
/// deliberately: the alternative is a client that reads, mutates and writes back, which loses
/// any concurrent change to a field it was not interested in.
/// </para>
/// </summary>
public sealed record UpdateGeographicPreferencesRequest
{
    /// <summary>
    /// Kilometres, or the client's sentinels — <c>-1</c> city-wide, <c>-2</c> country-wide.
    /// Both forms accepted.
    /// </summary>
    public double? SearchRadiusKm { get; init; }

    /// <summary><c>Kilometers</c> or <c>Miles</c>.</summary>
    public string? DistanceUnit { get; init; }

    /// <summary><c>Exact</c> or <c>Approximate</c>.</summary>
    public string? CoordinatePrecision { get; init; }

    public string? PreferredCountryCode { get; init; }

    public Guid? PreferredAreaId { get; init; }

    public string? HomeCountryCode { get; init; }

    public string? OriginCountryCode { get; init; }

    /// <summary><c>granted</c>, <c>denied</c> or <c>undetermined</c>.</summary>
    public string? PermissionStatus { get; init; }
}

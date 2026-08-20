namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// Where a provider is.
/// <para>
/// Field names are B4's, verbatim, including <c>distanceKm</c> — the client already renders that
/// name in three components and a fourth spelling would be one more thing to reconcile.
/// </para>
/// </summary>
public sealed record ProviderLocationResponse
{
    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>The client also calls this <c>neighborhood</c>. Same value.</summary>
    public string? AreaName { get; init; }

    public string? CityName { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>
    /// Distance from the caller's scope, in kilometres. Computed by Location, never by B7, and
    /// null when the request carried no point to measure from.
    /// </summary>
    public double? DistanceKm { get; init; }

    /// <summary>"Peckham, London" — the subtitle the client currently assembles by hand.</summary>
    public string? DisplayName { get; init; }
}

namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Sets where a provider is.
/// <para>
/// Every field optional, and resolved through B4 rather than trusted: coordinates without a city
/// become a city, a city without coordinates becomes an area, and a provider created from a map
/// pin lands in Peckham without B7 owning a gazetteer.
/// </para>
/// </summary>
public sealed record ProviderLocationRequest
{
    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

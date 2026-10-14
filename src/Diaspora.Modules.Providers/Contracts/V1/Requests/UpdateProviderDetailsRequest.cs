namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// <c>PATCH /providers/{id}/details</c>: what an owner most often needs to correct after
/// registering, and could not until 14 Oct 2026.
/// <para>
/// <b>Partial.</b> A property left out (null) is not touched. For a contact method, an empty
/// string removes it. The location fields travel together: send any of them and the provider is
/// placed again from what was sent, exactly as registration places it (catalogue path first,
/// then the pin, then the town), so search finds it where it now says it is.
/// </para>
/// </summary>
public sealed record UpdateProviderDetailsRequest
{
    public string? Description { get; init; }

    public string? Phone { get; init; }

    public string? WhatsApp { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>
/// Moves the member's active discovery location.
/// <para>
/// Every field is optional except the country, because the selector can produce a location
/// three ways: picking a catalogue node (sends <c>areaId</c>), detecting by GPS (sends
/// coordinates), or choosing a country on a settings screen (sends neither). All three are
/// legitimate and all three land here.
/// </para>
/// </summary>
public sealed record SetCurrentLocationRequest
{
    /// <summary>Preferred when the client has it: unambiguous, and links the snapshot.</summary>
    public Guid? AreaId { get; init; }

    public required string CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? State { get; init; }

    public string? CityName { get; init; }

    /// <summary>Also accepted as <c>areaName</c> by verticals; one value, two names.</summary>
    public string? Neighborhood { get; init; }

    public string? Street { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? DisplayName { get; init; }
}

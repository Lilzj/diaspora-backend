namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// Where something is, as the wire renders it.
/// <para>
/// Identifiers and names. The neighbourhood is B4's and its profile is B11's; this carries the key
/// so a client can ask either, and the cached names so a card renders without a second call.
/// </para>
/// </summary>
public sealed record LocationResponse
{
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    /// <summary>B4's area. The identifier every module on this platform already shares.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>The most specific place name available, for a one-line rendering.</summary>
    public required string DisplayName { get; init; }
}

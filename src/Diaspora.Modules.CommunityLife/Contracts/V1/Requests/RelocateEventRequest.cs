namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Move an event in space. Material, for the same reason a reschedule is.</summary>
public sealed record RelocateEventRequest
{
    public int? BaseRevision { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public EventVenueRequest? Venue { get; init; }

    public string? AttendanceMode { get; init; }
}

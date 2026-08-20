namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>When it can be moved into, and how much of it is left.</summary>
public sealed record HousingAvailabilityResponse
{
    /// <summary>Null means now.</summary>
    public DateTimeOffset? AvailableFrom { get; init; }

    /// <summary>Null means open-ended.</summary>
    public DateTimeOffset? AvailableUntil { get; init; }

    public required int TotalUnits { get; init; }

    /// <summary>What is left to reserve. The number a hostel page renders on every visit.</summary>
    public required int AvailableUnits { get; init; }

    public required int ReservedUnits { get; init; }

    public required int OccupiedUnits { get; init; }

    public int? MinimumStayNights { get; init; }

    public int? MaximumStayNights { get; init; }
}

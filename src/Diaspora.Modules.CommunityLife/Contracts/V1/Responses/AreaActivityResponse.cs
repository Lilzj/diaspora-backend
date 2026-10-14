namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// How much is happening in the area around a point (14 Oct 2026, Phase 2A of retiring the place
/// catalogue): counted from each event's venue and each organization's pin within the circle.
/// </summary>
public sealed record AreaActivityResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public required double RadiusKm { get; init; }

    /// <summary>The Google place name the client sent. Null when none was; never inferred.</summary>
    public string? Name { get; init; }

    /// <summary><see cref="Name"/>, or "Within 2 km of you".</summary>
    public required string DisplayName { get; init; }

    /// <summary>The window the event figures cover, in days back from now.</summary>
    public required int WindowDays { get; init; }

    /// <summary>Public events with a venue in the circle that opened within the window.</summary>
    public required int EventCount { get; init; }

    /// <summary>Public events with a venue in the circle that are running now or still to come.</summary>
    public required int UpcomingEventCount { get; init; }

    /// <summary>Public organizations pinned in the circle. Not bounded by the window.</summary>
    public required int OrganizationCount { get; init; }

    public required int TotalRsvps { get; init; }

    public required int TotalAttendances { get; init; }

    /// <summary>True when a count stopped at its reading cap, so the true figures are at least these.</summary>
    public required bool IsLowerBound { get; init; }
}

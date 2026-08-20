namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>The measured record for one organization.</summary>
public sealed record OrganizationAnalyticsResponse
{
    public required Guid OrganizationId { get; init; }

    public required int ViewCount { get; init; }

    public required int UniqueViewCount { get; init; }

    public required int TotalJoins { get; init; }

    public required int TotalDepartures { get; init; }

    public required int TotalRemovals { get; init; }

    public required int CurrentMemberCount { get; init; }

    public required int PeakMemberCount { get; init; }

    public required int EventsPublished { get; init; }

    public required int EventsCompleted { get; init; }

    public required int EventsCancelled { get; init; }

    public required int TotalRsvps { get; init; }

    public required int TotalAttendances { get; init; }

    public required int TotalNoShows { get; init; }

    /// <summary>Attendances by volunteers. Who an administrator needs to thank.</summary>
    public required int VolunteerAttendances { get; init; }

    /// <summary>
    /// Members retained out of everybody ever admitted. Rendered to administrators only — it reads
    /// badly out of context, because every student association loses its members on graduation.
    /// </summary>
    public double? RetentionRate { get; init; }

    public double? AttendanceRate { get; init; }
}

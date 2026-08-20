namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>How big an organization is and how much it does.</summary>
public sealed record MembershipStatisticsResponse
{
    public required int MemberCount { get; init; }

    /// <summary>Rendered to administrators only. What puts the badge on their dashboard.</summary>
    public int? PendingRequestCount { get; init; }

    public required int AdministratorCount { get; init; }

    public required int VolunteerCount { get; init; }

    public required int PublishedEventCount { get; init; }

    public required int CompletedEventCount { get; init; }
}

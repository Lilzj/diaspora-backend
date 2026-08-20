namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An organizer's view of one organization: the counts, the queue and what is next.</summary>
public sealed record OrganizationDashboardResponse
{
    public required OrganizationSummaryResponse Organization { get; init; }

    public required MembershipStatisticsResponse Statistics { get; init; }

    public required OrganizationAnalyticsResponse Analytics { get; init; }

    /// <summary>Requests waiting on a decision. The thing an administrator opened the screen for.</summary>
    public IReadOnlyList<MembershipResponse> PendingRequests { get; init; } = [];

    public IReadOnlyList<EventSummaryResponse> UpcomingEvents { get; init; } = [];

    public IReadOnlyList<EventSummaryResponse> RecentEvents { get; init; } = [];

    /// <summary>How complete the page is. Rendered as a prompt to finish it.</summary>
    public required double Completeness { get; init; }
}

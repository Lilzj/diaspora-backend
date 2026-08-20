namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What a landlord sees when they open the platform.
/// <para>
/// Ordered by what needs doing rather than by what exists: unanswered viewing requests first,
/// because a member is waiting on each one and every hour of silence is one they spend looking
/// somewhere else.
/// </para>
/// </summary>
public sealed record HousingDashboardResponse
{
    public required LandlordSummaryResponse Landlord { get; init; }

    public required LandlordStatisticsResponse Statistics { get; init; }

    /// <summary>Viewing requests nobody has answered. The most urgent thing on the screen.</summary>
    public required int PendingViewingRequests { get; init; }

    /// <summary>Reservations awaiting the landlord decision.</summary>
    public required int PendingReservations { get; init; }

    /// <summary>Viewings in the next seven days.</summary>
    public IReadOnlyList<HousingViewingResponse> UpcomingViewings { get; init; } = [];

    public required int DraftListings { get; init; }

    public required int PublishedListings { get; init; }

    public required int OccupiedListings { get; init; }

    /// <summary>Properties a moderator has flagged or hidden. Actionable, so it is on the dashboard.</summary>
    public required int FlaggedListings { get; init; }

    public required long TotalViewsLast30Days { get; init; }

    public IReadOnlyList<HousingListingCardResponse> RecentListings { get; init; } = [];
}

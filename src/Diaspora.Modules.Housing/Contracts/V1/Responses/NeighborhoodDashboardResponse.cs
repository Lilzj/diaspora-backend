namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// The editorial view of the neighbourhood estate.
/// <para>
/// Built for the people who keep the profiles honest: what is unwritten, what is unreviewed, and
/// what is waiting for a moderator. A profile nobody has looked at since a tram line opened is
/// quietly wrong, and this is the screen that surfaces it.
/// </para>
/// </summary>
public sealed record NeighborhoodDashboardResponse
{
    public required int TotalProfiles { get; init; }

    public required int ApprovedProfiles { get; init; }

    /// <summary>Profiles past the deployment review interval.</summary>
    public required int NeedingReview { get; init; }

    /// <summary>Profiles with listings in them and nothing written about them.</summary>
    public required int EmptyProfilesWithListings { get; init; }

    /// <summary>Insights waiting for a moderator.</summary>
    public required int PendingInsights { get; init; }

    /// <summary>The places with the most housing activity, so effort goes where members are.</summary>
    public IReadOnlyList<NeighborhoodSummaryResponse> MostActive { get; init; } = [];

    /// <summary>The places most in need of somebody writing about them.</summary>
    public IReadOnlyList<NeighborhoodSummaryResponse> LeastComplete { get; init; } = [];
}

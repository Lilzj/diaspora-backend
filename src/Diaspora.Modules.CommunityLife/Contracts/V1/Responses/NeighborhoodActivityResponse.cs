namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// How much is happening in one neighbourhood.
/// <para>
/// Keyed by B4's area identifier. The place name comes from Location and the profile from Housing;
/// a copy kept here would be the one that goes stale.
/// </para>
/// </summary>
public sealed record NeighborhoodActivityResponse
{
    public required Guid AreaId { get; init; }

    /// <summary>Passed through from B11's neighbourhood directory, when it has a profile.</summary>
    public string? Name { get; init; }

    public required int EventCount { get; init; }

    public required int OrganizationCount { get; init; }

    public required int TotalRsvps { get; init; }

    public required int TotalAttendances { get; init; }
}

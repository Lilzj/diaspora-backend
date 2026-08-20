namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>
/// How much is happening in one neighbourhood.
/// <para>
/// Keyed by B4's area identifier and carrying no place name, deliberately: the name belongs to
/// Location and the profile to Housing, and a copy kept here would be the one that goes stale.
/// </para>
/// </summary>
public sealed record NeighborhoodActivity(
    Guid AreaId,
    int EventCount,
    int TotalRsvps,
    int TotalAttendances);

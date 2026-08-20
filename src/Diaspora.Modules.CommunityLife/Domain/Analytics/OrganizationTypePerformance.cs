namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>How one kind of organization is growing. A projection, computed and never stored.</summary>
public sealed record OrganizationTypePerformance(
    string OrganizationTypeKey,
    int OrganizationCount,
    int TotalMembers,
    int TotalJoins,
    int TotalDepartures,
    int EventsPublished,
    int EventsCompleted)
{
    /// <summary>Members retained out of everybody ever admitted, across the whole type.</summary>
    public double? RetentionRate =>
        TotalJoins == 0 ? null : (double)TotalMembers / TotalJoins;
}

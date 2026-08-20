using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// A member reported an organization.
/// <para>
/// Carries no reporter identity, for the same reason the event report does not.
/// </para>
/// </summary>
public sealed record OrganizationReportedIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Reason { get; init; }

    public required int TotalReports { get; init; }

    public required bool WasFlagged { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// The subject of the report, never the person who filed it. See the summary: the reporter's
    /// identity does not cross this boundary, and adding a recipient does not change that.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}

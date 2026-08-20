using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// A member reported an event.
/// <para>
/// <b>Carries no reporter identity.</b> A report is filed by somebody who may be in the same
/// community as the person they reported — the same congregation, the same association — and a
/// contract that named them would put that on every consumer's queue. The moderator sees who filed
/// it; nothing downstream does.
/// </para>
/// </summary>
public sealed record EventReportedIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Why, as its name. <c>Duplicate</c> is the common one and is not abuse.</summary>
    public required string Reason { get; init; }

    /// <summary>How many distinct members have now reported it.</summary>
    public required int TotalReports { get; init; }

    /// <summary>Whether this report crossed the auto-flag threshold.</summary>
    public required bool WasFlagged { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }

    /// <summary>
    /// The member who organized it — B13.1.
    /// <para>
    /// <b>The subject of the report, not the person who filed it.</b> The reporter is still
    /// absent from this payload and always will be, for the reason in the summary. This is the
    /// organizer, so a consumer can tell them their event was flagged once
    /// <see cref="WasFlagged"/> is true — which is the point at which the platform is acting
    /// against them and owes them notice.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the event could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}

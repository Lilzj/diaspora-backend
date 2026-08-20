using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Somebody came through the door.
/// <para>
/// <b>The attendance record other modules build reputation on.</b> It is published on check-in
/// rather than on settlement so an organizer's live dashboard is live, and it carries the method so
/// a manual entry is distinguishable from a scanned one — which is the distinction a future QR
/// implementation needs to exist before it ships, not after.
/// </para>
/// </summary>
public sealed record EventCheckInCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid RsvpId { get; init; }

    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    /// <summary>How they were identified — <c>Manual</c>, <c>Organizer</c>, <c>SelfConfirmed</c>.</summary>
    public required string Method { get; init; }

    /// <summary>Who worked the door. Null when the attendee confirmed their own arrival.</summary>
    public Guid? RecordedByUserId { get; init; }

    public required int PartySize { get; init; }

    /// <summary>Everybody through the door so far. What a live dashboard renders.</summary>
    public required int CheckedInCount { get; init; }

    public required DateTimeOffset CheckedInAt { get; init; }
}

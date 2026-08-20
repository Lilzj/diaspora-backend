using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Somebody who was coming is no longer coming.
/// <para>
/// <see cref="Reason"/> is the field that matters, and it is a decline reason rather than free
/// text. Somebody who withdrew a fortnight in advance, somebody an organizer removed and somebody
/// whose event was cancelled are three different facts, and a consumer computing a reliability
/// signal that could not tell them apart would punish the member for the organizer's decision.
/// </para>
/// </summary>
public sealed record RsvpWithdrawnIntegrationEvent : IntegrationEvent
{
    public required Guid RsvpId { get; init; }

    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    public required int PartySize { get; init; }

    /// <summary>
    /// Why, as its name — <c>WithdrawnByAttendee</c>, <c>RemovedByOrganizer</c>,
    /// <c>EventCancelled</c>, <c>DidNotAttend</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// How long before the event it happened. What separates a courteous withdrawal from a
    /// last-minute one, without a consumer having to hold the event's schedule.
    /// </summary>
    public double? HoursBeforeStart { get; init; }

    public required int GoingCount { get; init; }

    public int? RemainingPlaces { get; init; }

    public required DateTimeOffset WithdrawnAt { get; init; }

    /// <summary>
    /// The member who organized it — B13.1.
    /// <para>
    /// A place came back, and the person who needs to know is the one counting places. Carried
    /// alongside the attendee for the same reason the submission carries both: the two sides of a
    /// commitment are two different messages, and neither should require a call into this module
    /// to address.
    /// </para>
    /// <para>
    /// <see cref="Reason"/> still decides whether anything is sent at all — an organizer who
    /// removed somebody does not need telling that they removed somebody.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the event could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}

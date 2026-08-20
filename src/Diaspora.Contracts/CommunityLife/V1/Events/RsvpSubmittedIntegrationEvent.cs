using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Somebody said they were coming.
/// <para>
/// <b>Published for a commitment, never for a passing interest.</b> An Interested response is
/// somebody browsing, and a consumer notifying an organizer about every one of those would make the
/// signal worthless. The relay drops them.
/// </para>
/// <para>
/// Carries the attendee's identifier because the organizer legitimately needs to know who is
/// coming, and carries nothing else about them: a name, a photograph and a phone number are the
/// organizer's to look up through Identity, with that module's own rules applied.
/// </para>
/// </summary>
public sealed record RsvpSubmittedIntegrationEvent : IntegrationEvent
{
    public required Guid RsvpId { get; init; }

    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    /// <summary>The status, as its name — <c>Going</c> or <c>Waitlisted</c>.</summary>
    public required string Status { get; init; }

    /// <summary>How many people are coming, including the member.</summary>
    public required int PartySize { get; init; }

    /// <summary>Places committed after this response.</summary>
    public required int GoingCount { get; init; }

    /// <summary>Places left, or null when the event is uncapped.</summary>
    public int? RemainingPlaces { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }

    /// <summary>
    /// The member who organized it — B13.1.
    /// <para>
    /// <b>The enrichment this contract most needed.</b> "Somebody is coming" is an organizer's
    /// message and the payload named only the attendee, so a consumer could confirm the RSVP to
    /// the person who made it and could not tell the person running the event. Resolving the
    /// organizer downstream would have meant Communication querying Community Life.
    /// </para>
    /// <para>
    /// One person, and still no guest list: <see cref="AttendeeUserId"/> is who responded and
    /// <see cref="GoingCount"/> is how many are coming. Who else holds a place remains the
    /// organizer's to look up.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the event could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }

    /// <summary>
    /// When the event this response commits to begins — B13.3.
    /// <para>
    /// <b>The field a reminder cannot be scheduled without.</b> Communication schedules a reminder by
    /// applying a rule's offset to the moment the event names — the interview time, the viewing
    /// appointment — which is how <c>jobs.interview.reminder</c> and <c>housing.viewing.reminder</c>
    /// have worked since B13. This contract named no moment, so the same rule applied to an RSVP
    /// resolved to "no subject moment" and, by the dispatcher's documented rule, sent immediately:
    /// a member would have been reminded about an event the instant they said they were coming.
    /// Rather than a reminder that fires at the wrong time, there was no reminder at all.
    /// </para>
    /// <para>
    /// <b>Costs nothing to publish.</b> The relay was already handed this value — it computes
    /// <c>HoursBeforeStart</c> on the withdrawal contract from it — so no new read, no new query and
    /// no aggregate is loaded to fill it in.
    /// </para>
    /// <para>
    /// <b>It is the start at the time of the response, and it is not authoritative later.</b> An
    /// event that moves publishes <c>EventOccurrenceRescheduledIntegrationEvent</c>, which is what a
    /// consumer must act on; nothing re-sends this one. A scheduled reminder built on a stale moment
    /// is why the reschedule contract carries both the previous and the current start.
    /// </para>
    /// <para>
    /// <b>Appended, never inserted.</b> Property order is part of the serialised shape, and a field
    /// added in the middle changes how every previously-written outbox row deserialises.
    /// </para>
    /// <para>Null on events published before B13.3.</para>
    /// </summary>
    public DateTimeOffset? EventStartsAt { get; init; }

    /// <summary>
    /// What the event is called — B13.3.
    /// <para>
    /// <b>The one piece of the subject's own content this contract carries, and it is carried for a
    /// reminder rather than for display.</b> A member with three responses in a week reads "Lagos
    /// Supper Club is tomorrow", not "an event you are going to is tomorrow" — and the alternative
    /// was Communication asking Community Life what an event is called, which is the coupling the
    /// whole arrangement exists without.
    /// </para>
    /// <para>
    /// <b>It is the title at the moment of the response and is never corrected.</b> An event that is
    /// renamed publishes <c>EventUpdatedIntegrationEvent</c> with the new one; a reminder already
    /// scheduled keeps the words it was written with, which is the ordinary consequence of rendering
    /// a message when the fact happened rather than when it is delivered.
    /// </para>
    /// <para>Appended, never inserted. Null on events published before B13.3.</para>
    /// </summary>
    public string? EventTitle { get; init; }
}

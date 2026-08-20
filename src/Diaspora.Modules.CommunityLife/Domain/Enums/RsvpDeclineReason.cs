namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Why an RSVP ended without an attendance.
/// <para>
/// Recorded rather than inferred, because the four causes have entirely different meanings to an
/// organizer. An event that was cancelled by its organizer should not damage anybody's attendance
/// record, and a member who withdrew a fortnight in advance is not the same as one who did not
/// turn up.
/// </para>
/// </summary>
public enum RsvpDeclineReason
{
    /// <summary>Still live, or ended in an attendance.</summary>
    None = 0,

    /// <summary>The member withdrew.</summary>
    WithdrawnByAttendee = 1,

    /// <summary>The organizer removed them from the list.</summary>
    RemovedByOrganizer = 2,

    /// <summary>The event was called off. Never counts against the member.</summary>
    EventCancelled = 3,

    /// <summary>They said they were coming and did not.</summary>
    DidNotAttend = 4
}

namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>
/// The change one response makes to an event's four attendance counters.
/// <para>
/// <b>The delta counterpart of <see cref="RsvpPlaceTotals"/>.</b> The recount says where the numbers
/// should be; this says how far to move them. Both are derived from the same bucket predicates in
/// <c>RsvpStatusTransitions</c>, which is the only reason a sweep's reconciliation and a handler's
/// settlement can be trusted to agree. Two hand-written copies of "which statuses hold a place"
/// would drift the first time a status was added, and the symptom would be an organizer told they
/// have places left when they do not.
/// </para>
/// </summary>
public sealed record RsvpPlaceMovement(int Going, int Interested, int Waitlisted, int CheckedIn)
{
    public static RsvpPlaceMovement None { get; } = new(0, 0, 0, 0);

    /// <summary>Whether the movement is worth a write at all.</summary>
    public bool IsEmpty => Going == 0 && Interested == 0 && Waitlisted == 0 && CheckedIn == 0;
}

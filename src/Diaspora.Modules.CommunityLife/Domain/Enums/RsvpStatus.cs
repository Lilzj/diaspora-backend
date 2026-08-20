namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where one member stands with one event.
/// <para>
/// <b>The whole journey is kept, not just the current value.</b> An RSVP that went
/// Interested → Going → Cancelled tells an organizer something an RSVP that is merely Cancelled
/// does not, and no-show rate — which the brief asks for by name — is uncomputable without the
/// distinction between somebody who cancelled and somebody who simply did not come.
/// </para>
/// <para>
/// <see cref="Waitlisted"/> is not in the brief and is here anyway: a capacity that is reached is
/// the normal case for anything popular, and the alternative to a waiting list is refusing the
/// RSVP outright, which loses the member and the signal at the same time.
/// </para>
/// </summary>
public enum RsvpStatus
{
    /// <summary>Might come. Costs nothing and commits to nothing.</summary>
    Interested = 0,

    /// <summary>Coming. Holds a place against the capacity.</summary>
    Going = 1,

    /// <summary>Coming, if a place frees up. Holds no capacity.</summary>
    Waitlisted = 2,

    /// <summary>Arrived, and somebody at the door said so.</summary>
    CheckedIn = 3,

    /// <summary>Was there. Settled after the event, from the check-in or by the organizer.</summary>
    Attended = 4,

    /// <summary>Said they were coming and withdrew. Frees the place.</summary>
    Cancelled = 5,

    /// <summary>Said they were coming and did not. Settled by the sweep after the event ends.</summary>
    NoShow = 6
}

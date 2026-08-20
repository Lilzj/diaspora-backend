namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where an event sits in its lifecycle.
/// <para>
/// <b>The brief draws eight states and this enum has eight, but two of them are not what they look
/// like.</b> "Open for RSVP" is not a state an organizer sets — it is a fact about the registration
/// deadline and the capacity, and modelling it as a status would mean a background job had to move
/// every event twice a day or the door would never open. It is <see cref="Published"/> plus a
/// derived predicate on the aggregate. "Check-in" is the same: check-in opens on a schedule, and an
/// event sitting in a Checkin status the morning after would be lying about itself.
/// </para>
/// <para>
/// What is left is the set an organizer or the clock actually moves through, and those are here.
/// <see cref="InProgress"/> and <see cref="Completed"/> are set by the sweep from the event's own
/// schedule, which is the only honest source: an organizer busy running a festival is not going to
/// press a button.
/// </para>
/// </summary>
public enum EventStatus
{
    /// <summary>Being written. Visible to its organizers and nobody else.</summary>
    Draft = 0,

    /// <summary>Announced. Discoverable, and open for RSVP if it takes them.</summary>
    Published = 1,

    /// <summary>Happening now. Set by the sweep from the start time.</summary>
    InProgress = 2,

    /// <summary>Over. Set by the sweep from the end time.</summary>
    Completed = 3,

    /// <summary>Called off. Terminal, and everyone who was coming is told.</summary>
    Cancelled = 4,

    /// <summary>Filed away. Reversible only through a restore into draft.</summary>
    Archived = 5
}

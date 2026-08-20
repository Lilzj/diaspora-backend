namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where a series sits in its lifecycle.
/// <para>
/// <b>Five states, and <see cref="Paused"/> is the one that does not exist on a single event.</b>
/// A committee that suspends its monthly meeting over the summer has not cancelled the series and
/// has not finished it; pausing stops the horizon being topped up and stops future occurrences being
/// published, while leaving everything already announced exactly where it is. Folding that into
/// cancellation would tell every attendee of every future occurrence that the meeting is off.
/// </para>
/// <para>
/// <b><see cref="Completed"/> is set by the sweep, not by an organizer</b>, for the same reason an
/// event completes on its end time: a series whose rule has run out has run out whether or not
/// anybody says so.
/// </para>
/// </summary>
public enum EventSeriesStatus
{
    /// <summary>Being written. Its occurrences exist as drafts and nobody can see them.</summary>
    Draft = 0,

    /// <summary>Announced. Occurrences are published as the horizon reaches them.</summary>
    Published = 1,

    /// <summary>Suspended. Nothing new is generated; what is already announced stands.</summary>
    Paused = 2,

    /// <summary>The rule has run out. Terminal.</summary>
    Completed = 3,

    /// <summary>Called off. Every future occurrence is cancelled with it. Terminal.</summary>
    Cancelled = 4
}

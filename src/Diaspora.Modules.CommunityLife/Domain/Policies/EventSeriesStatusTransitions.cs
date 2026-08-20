using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The series lifecycle, as a machine.
/// <para>
/// <b>Pausing is reversible and cancelling is not</b>, which is the whole reason both exist. A
/// committee that stops meeting over the summer resumes in September; one that has wound up does
/// not. Collapsing the two would mean the only way to stop generating dates was to tell every
/// attendee of every announced occurrence that it was off.
/// </para>
/// <para>
/// <b><see cref="EventSeriesStatus.Completed"/> leads nowhere but is not a failure state.</b> A rule
/// that ended on a date or after a count has finished, and reopening it would produce occurrences
/// past a boundary the organizer set deliberately. Running it again is a new series.
/// </para>
/// </summary>
public static class EventSeriesStatusTransitions
{
    private static readonly Dictionary<EventSeriesStatus, EventSeriesStatus[]> Allowed = new()
    {
        [EventSeriesStatus.Draft] =
        [
            EventSeriesStatus.Published,
            EventSeriesStatus.Cancelled
        ],
        [EventSeriesStatus.Published] =
        [
            EventSeriesStatus.Paused,
            EventSeriesStatus.Completed,
            EventSeriesStatus.Cancelled
        ],
        [EventSeriesStatus.Paused] =
        [
            EventSeriesStatus.Published,
            EventSeriesStatus.Completed,
            EventSeriesStatus.Cancelled
        ],
        [EventSeriesStatus.Completed] = [],
        [EventSeriesStatus.Cancelled] = []
    };

    public static bool CanTransition(EventSeriesStatus from, EventSeriesStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Whether the horizon may still be topped up with new occurrences.</summary>
    public static bool GeneratesOccurrences(EventSeriesStatus status) =>
        status is EventSeriesStatus.Draft or EventSeriesStatus.Published;

    /// <summary>Whether newly generated occurrences are announced rather than drafted.</summary>
    public static bool PublishesOccurrences(EventSeriesStatus status) =>
        status is EventSeriesStatus.Published;

    /// <summary>Whether the rule and the template may still be edited.</summary>
    public static bool IsEditable(EventSeriesStatus status) =>
        status is EventSeriesStatus.Draft or EventSeriesStatus.Published or EventSeriesStatus.Paused;

    public static bool IsTerminal(EventSeriesStatus status) =>
        status is EventSeriesStatus.Completed or EventSeriesStatus.Cancelled;
}

using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Observability;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// The one derived attendance measure, shared by the two handlers that can fill an event.
/// <para>
/// <b>Only meaningful on the transition into full.</b> Recording it on every response afterwards
/// would report the same event filling over and over and drag the platform's median toward zero, so
/// the caller passes in whether the event was already full before it settled.
/// </para>
/// <para>
/// An event that has never been published has no clock to measure from — an organizer filling a
/// private guest list before announcing anything did not fill it in no time at all.
/// </para>
/// </summary>
internal static class AttendanceMetrics
{
    public static void RecordTimeToFill(
        CommunityEvent communityEvent, bool wasFull, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        if (wasFull
            || !communityEvent.IsFull
            || communityEvent.FirstPublishedAt is not { } publishedAt)
        {
            return;
        }

        CommunityLifeTelemetry.TimeToFillHours.Record(
            (now - publishedAt).TotalHours,
            new KeyValuePair<string, object?>("category", communityEvent.CategoryKey));
    }
}

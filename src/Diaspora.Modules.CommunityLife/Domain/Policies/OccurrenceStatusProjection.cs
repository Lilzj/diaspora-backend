using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// What a client renders for one dated instance, computed from what the event already says.
/// <para>
/// <b>Derived rather than stored, and that is the same call the module made about "open for
/// RSVP".</b> An occurrence's lifecycle is <see cref="EventStatus"/>; a second stored status would
/// be a second thing to keep true, and the two would disagree the first hour the completion sweep
/// moved one and not the other. There is one row of truth and one function that reads it.
/// </para>
/// </summary>
public static class OccurrenceStatusProjection
{
    /// <summary>The occurrence status for an event that belongs to a series.</summary>
    public static OccurrenceStatus For(CommunityEvent communityEvent)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        return For(
            communityEvent.Status,
            communityEvent.OriginalStartsAt is not null,
            communityEvent.IsDeleted);
    }

    /// <summary>
    /// The projection over its three inputs, so a query can compute it without materialising an
    /// aggregate.
    /// </summary>
    /// <param name="status">The event's own lifecycle state.</param>
    /// <param name="wasMoved">Whether the occurrence carries a generated start it no longer sits on.</param>
    /// <param name="isDeleted">
    /// Whether the row is soft-deleted. Reported as cancelled rather than as a fourth value: a
    /// client showing a series has to render "this date is not happening" either way, and the
    /// difference between a deletion and a cancellation is a moderation fact, not a calendar one.
    /// </param>
    public static OccurrenceStatus For(EventStatus status, bool wasMoved, bool isDeleted = false)
    {
        if (isDeleted)
        {
            return OccurrenceStatus.Cancelled;
        }

        return status switch
        {
            EventStatus.Cancelled => OccurrenceStatus.Cancelled,
            EventStatus.Completed => OccurrenceStatus.Completed,

            // An archived occurrence is one nobody can reach and nobody is coming to. Rendering it
            // as scheduled would put a date on a calendar that resolves to nothing.
            EventStatus.Archived => OccurrenceStatus.Cancelled,
            _ => wasMoved ? OccurrenceStatus.Rescheduled : OccurrenceStatus.Scheduled
        };
    }
}

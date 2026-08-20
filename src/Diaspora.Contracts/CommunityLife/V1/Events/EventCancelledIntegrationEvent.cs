using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An event was called off.
/// <para>
/// <b>The one contract in this module with an operational obligation attached.</b> Everybody who
/// said they were coming has to be told, and <see cref="AffectedAttendeeCount"/> travels with it so
/// a consumer can size that work without loading a guest list it has no business seeing.
/// </para>
/// <para>
/// It carries the start time, because a cancellation notice that does not say which occasion is
/// being cancelled is useless to somebody who follows several.
/// </para>
/// </summary>
public sealed record EventCancelledIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required int AffectedAttendeeCount { get; init; }

    /// <summary>Whether a moderator did it, as opposed to the organizer.</summary>
    public required bool IsModerationAction { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }

    /// <summary>
    /// The member who organized it — B13.1.
    /// <para>
    /// <b>Carried because a cancellation is not always the organizer's own doing.</b> A moderator
    /// can call an event off, and an organizer who finds out from an attendee has been failed
    /// twice. <see cref="IsModerationAction"/> is what tells the two apart.
    /// </para>
    /// <para>
    /// It is one person, and the attendees are still not on the payload:
    /// <see cref="AffectedAttendeeCount"/> sizes that fan-out without naming anybody, exactly as
    /// before.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the event could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }

    /// <summary>
    /// The repeating event this date belonged to — B12.1.
    /// <para>
    /// <b>What lets a consumer tell "the meetup is cancelled" from "this week's meetup is
    /// cancelled".</b> The two need different words and the second must not imply the first, which
    /// is the single most consequential distinction a recurring event introduces to a notification.
    /// </para>
    /// <para>Appended, never inserted; see the publication contract's note.</para>
    /// </summary>
    public Guid? SeriesId { get; init; }

    /// <summary>
    /// Whether the whole pattern was called off, as distinct from this one date. Only ever true
    /// alongside a <see cref="SeriesId"/>.
    /// </summary>
    public bool IsSeriesCancellation { get; init; }
}

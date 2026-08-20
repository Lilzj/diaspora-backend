namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// One dated instance of a series, as a card renders it.
/// <para>
/// <b><see cref="OccurrenceId"/> is the event identifier, and that is deliberate.</b> An occurrence
/// <em>is</em> an event here, so everything that already works against an event — the detail screen,
/// the RSVP, the check-in, the attendee list, the analytics — works against an occurrence without a
/// second contract. A client holding one of these can call <c>/events/{id}</c> with it.
/// </para>
/// </summary>
public sealed record EventOccurrenceResponse
{
    /// <summary>Also the event identifier. See the type summary.</summary>
    public required Guid OccurrenceId { get; init; }

    public required Guid SeriesId { get; init; }

    /// <summary>Its ordinal in the series. Stable: cancelling one does not renumber the rest.</summary>
    public required int Sequence { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required string TimeZoneId { get; init; }

    /// <summary>The local date in the series' zone. The rule's own vocabulary.</summary>
    public required DateOnly LocalDate { get; init; }

    /// <summary><c>Scheduled</c>, <c>Rescheduled</c>, <c>Cancelled</c> or <c>Completed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The event's own lifecycle state, for a client that needs the finer distinction.</summary>
    public required string EventStatus { get; init; }

    /// <summary>Set when the occurrence was moved — the instant the rule generated.</summary>
    public DateTimeOffset? OriginalStartsAt { get; init; }

    public string? CancellationReason { get; init; }

    public required LocationResponse Location { get; init; }

    public string? VenueName { get; init; }

    public required EventCapacityResponse Capacity { get; init; }

    /// <summary>The caller's own response to this date, when they have one.</summary>
    public string? ViewerRsvpStatus { get; init; }

    /// <summary>True for the soonest date the series still has. Decided here, never by the client.</summary>
    public required bool IsNext { get; init; }

    public required int Revision { get; init; }
}

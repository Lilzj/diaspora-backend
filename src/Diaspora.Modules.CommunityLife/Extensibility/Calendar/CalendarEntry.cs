namespace Diaspora.Modules.CommunityLife.Extensibility.Calendar;

/// <summary>
/// One row on a community calendar.
/// <para>
/// Deliberately not an event. A calendar shows events and the things around them — a national
/// holiday, a religious observance, a school term boundary — and modelling those as events would
/// make somebody able to RSVP to Eid.
/// </para>
/// </summary>
public sealed record CalendarEntry
{
    /// <summary>The contributing source's own identifier for the entry.</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>
    /// The IANA zone the entry's times are local to, when the source has one.
    /// <para>
    /// An instant alone cannot say that a Lagos event is at 7pm to a member reading the calendar in
    /// London, or which day an all-day entry falls on there. An event's own page shows the venue's
    /// time, so a calendar showing the reader's would disagree with it (RMD-008). Null when a
    /// source's rows are not tied to a place's clock.
    /// </para>
    /// </summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Whether it occupies a whole day rather than a time. Most holidays do.</summary>
    public bool IsAllDay { get; init; }

    /// <summary>What kind of thing it is, in the source's own vocabulary.</summary>
    public required string EntryType { get; init; }

    public string? Description { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>The event this entry corresponds to, when it is one of ours.</summary>
    public Guid? EventId { get; init; }
}

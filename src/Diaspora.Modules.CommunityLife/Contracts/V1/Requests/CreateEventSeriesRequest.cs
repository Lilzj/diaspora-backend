namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Set up a repeating event.
/// <para>
/// <b>The time is a local wall-clock time and a zone, not an instant.</b> "Seven in the evening,
/// Europe/London" is what an organizer means, and it stays seven in the evening across a
/// daylight-saving boundary; an instant would not, and the meeting would move by an hour twice a
/// year without anybody changing anything.
/// </para>
/// <para>
/// Lands as a draft unless <see cref="Publish"/> says otherwise, and publishing still runs the same
/// readiness check a single event's does.
/// </para>
/// </summary>
public sealed record CreateEventSeriesRequest
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    /// <summary>A key from the event-category catalogue.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>The organizing organization. Optional, as it is on a single event.</summary>
    public Guid? OrganizationId { get; init; }

    public string? HostName { get; init; }

    /// <summary>The local date the pattern starts from, in the series' own zone.</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>The local time of day every occurrence starts at. <c>HH:mm</c>.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>
    /// How long each occurrence runs, in minutes. Minutes rather than an end time so an event may
    /// cross midnight without its end appearing to precede its start.
    /// </summary>
    public required int DurationMinutes { get; init; }

    /// <summary>An IANA identifier — <c>Europe/London</c>, <c>Africa/Lagos</c>. Defaults to UTC.</summary>
    public string? TimeZoneId { get; init; }

    public required RecurrenceRuleRequest Recurrence { get; init; }

    /// <summary><c>InPerson</c>, <c>Online</c>, <c>Hybrid</c>. Defaults to in person.</summary>
    public string? AttendanceMode { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public EventVenueRequest? Venue { get; init; }

    /// <summary>Stamped onto every occurrence. Null means uncapped.</summary>
    public int? Capacity { get; init; }

    /// <summary>Defaults to the category's own setting, as a single event's does.</summary>
    public bool? RequiresRsvp { get; init; }

    public bool IsFamilyFriendly { get; init; }

    public bool IsChildFriendly { get; init; }

    public bool IsStudentFocused { get; init; }

    public bool IsFree { get; init; } = true;

    public string? DressCode { get; init; }

    public string? AccessibilityNotes { get; init; }

    public ContactDetailsRequest? Contact { get; init; }

    /// <summary>Publish the series, and every occurrence already written, in the same request.</summary>
    public bool Publish { get; init; }
}

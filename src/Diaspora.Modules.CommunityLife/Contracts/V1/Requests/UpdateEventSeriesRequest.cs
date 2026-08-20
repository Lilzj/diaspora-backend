namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Edit a repeating event.
/// <para>
/// <b><see cref="Scope"/> is mandatory and is the caller's decision, not the server's.</b> "Move the
/// Tuesday meetup to seven o'clock" means this week, or from now on, or every week — three different
/// acts with three different sets of people to tell, and only the person editing knows which. A
/// server that guessed would move a hundred announced dates or none.
/// </para>
/// <para>
/// Every content field is optional and null means "leave it alone", as everywhere else in B12.
/// </para>
/// </summary>
public sealed record UpdateEventSeriesRequest
{
    /// <summary><c>ThisOccurrence</c>, <c>ThisAndFuture</c> or <c>EntireSeries</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Which date the scope is measured from. Required for <c>ThisOccurrence</c> and
    /// <c>ThisAndFuture</c>, ignored for <c>EntireSeries</c>.
    /// </summary>
    public Guid? OccurrenceId { get; init; }

    public int? BaseRevision { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? HostName { get; init; }

    /// <summary>The new local time of day. Applies from the scope's first occurrence onward.</summary>
    public TimeOnly? StartTime { get; init; }

    public int? DurationMinutes { get; init; }

    public string? TimeZoneId { get; init; }

    /// <summary>
    /// A replacement rule. Rejected on <c>ThisOccurrence</c>: changing the pattern is not something
    /// one date can do, and accepting it there would apply a series-wide change under a scope that
    /// promised not to.
    /// </summary>
    public RecurrenceRuleRequest? Recurrence { get; init; }

    public string? AttendanceMode { get; init; }

    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public EventVenueRequest? Venue { get; init; }

    public int? Capacity { get; init; }

    public bool? RequiresRsvp { get; init; }

    public bool? IsAttendeeListPublic { get; init; }

    public ContactDetailsRequest? Contact { get; init; }
}

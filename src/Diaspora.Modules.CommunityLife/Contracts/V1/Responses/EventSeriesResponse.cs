namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A repeating event, in full. What a series screen renders.</summary>
public sealed record EventSeriesResponse
{
    public required Guid SeriesId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    public required string CategoryKey { get; init; }

    public string? CategoryName { get; init; }

    public IReadOnlyList<string> Facets { get; init; } = [];

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public string? OrganizationSlug { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public string? HostName { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    public required RecurrenceRuleResponse Recurrence { get; init; }

    /// <summary>The local date the pattern starts from.</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>The local wall-clock time every occurrence starts at.</summary>
    public required TimeOnly StartTime { get; init; }

    public required int DurationMinutes { get; init; }

    public required string TimeZoneId { get; init; }

    public required string AttendanceMode { get; init; }

    public required LocationResponse Location { get; init; }

    public required EventVenueResponse Venue { get; init; }

    public required ContactResponse Contact { get; init; }

    /// <summary>The ceiling stamped onto each occurrence. Null means uncapped.</summary>
    public int? Capacity { get; init; }

    public required bool RequiresRsvp { get; init; }

    public required bool IsAttendeeListPublic { get; init; }

    public required bool IsFree { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsChildFriendly { get; init; }

    public required bool IsStudentFocused { get; init; }

    public string? DressCode { get; init; }

    public string? AccessibilityNotes { get; init; }

    /// <summary><c>Draft</c>, <c>Published</c>, <c>Paused</c>, <c>Completed</c>, <c>Cancelled</c>.</summary>
    public required string Status { get; init; }

    public required string Visibility { get; init; }

    /// <summary>
    /// The next date, so a card renders without a second call.
    /// <para>
    /// Null on a series whose dates have all been held, or one that has been called off.
    /// </para>
    /// </summary>
    public EventOccurrenceResponse? NextOccurrence { get; init; }

    /// <summary>How many dates exist as real, addressable occurrences right now.</summary>
    public required int OccurrenceCount { get; init; }

    public required int CompletedCount { get; init; }

    public required int CancelledOccurrenceCount { get; init; }

    /// <summary>
    /// The local date occurrences have been written up to.
    /// <para>
    /// Exposed rather than hidden because a never-ending series genuinely has more dates than it has
    /// rows, and a client that showed "12 occurrences" for a weekly service without saying how far
    /// ahead that counts would be stating something false.
    /// </para>
    /// </summary>
    public DateOnly? MaterialisedThrough { get; init; }

    public string? CancellationReason { get; init; }

    public required int Revision { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

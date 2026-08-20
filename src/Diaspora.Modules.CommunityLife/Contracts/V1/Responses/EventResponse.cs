namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An event, in full. What a detail screen renders.</summary>
public sealed record EventResponse
{
    public required Guid EventId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public string? CategoryName { get; init; }

    /// <summary>The search facets the category belongs to — religious, cultural, business.</summary>
    public IReadOnlyList<string> Facets { get; init; } = [];

    public required string Summary { get; init; }

    public string? Description { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public string? OrganizationSlug { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public string? HostName { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    public required EventScheduleResponse Schedule { get; init; }

    public required string AttendanceMode { get; init; }

    public required LocationResponse Location { get; init; }

    public required EventVenueResponse Venue { get; init; }

    public required EventCapacityResponse Capacity { get; init; }

    public required ContactResponse Contact { get; init; }

    public IReadOnlyList<AgendaItemResponse> Agenda { get; init; } = [];

    public IReadOnlyList<EventSpeakerResponse> Speakers { get; init; } = [];

    public IReadOnlyList<MediaReferenceResponse> Media { get; init; } = [];

    public IReadOnlyList<GuideReferenceResponse> Guides { get; init; } = [];

    public IReadOnlyList<ProviderReferenceResponse> Providers { get; init; } = [];

    public IReadOnlyList<LinkedResourceResponse> LinkedResources { get; init; } = [];

    public Guid? CoverMediaId { get; init; }

    public required string Status { get; init; }

    public required string Visibility { get; init; }

    public required string ModerationState { get; init; }

    public required bool RequiresRsvp { get; init; }

    /// <summary>Derived from the deadline, the capacity and the clock — never a stored status.</summary>
    public required bool AcceptsRsvp { get; init; }

    public required bool AcceptsCheckIn { get; init; }

    public required bool IsAttendeeListPublic { get; init; }

    public required bool IsFree { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsChildFriendly { get; init; }

    public required bool IsStudentFocused { get; init; }

    public string? DressCode { get; init; }

    public string? AccessibilityNotes { get; init; }

    public required bool IsFeatured { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>The caller's own response, when they have one.</summary>
    public string? ViewerRsvpStatus { get; init; }

    public Guid? ViewerRsvpId { get; init; }

    public required int ViewCount { get; init; }

    public required int Revision { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // ── Series membership (B12.1) ───────────────────────────────────────────────────────────
    //
    // Appended rather than inserted. The property order of a record is the serialised order, and a
    // field inserted mid-record shifts every one after it for any consumer reading positionally —
    // the trap B13.1 recorded. New fields go at the end, always.

    /// <summary>The repeating event this is one date of, when it is one.</summary>
    public Guid? SeriesId { get; init; }

    /// <summary>Its ordinal in that series. Stable: cancelling one date does not renumber the rest.</summary>
    public int? SeriesSequence { get; init; }

    /// <summary>
    /// <c>Scheduled</c>, <c>Rescheduled</c>, <c>Cancelled</c> or <c>Completed</c>. Null on an event
    /// that belongs to no series, because a one-off occasion has no occurrence status to have.
    /// </summary>
    public string? OccurrenceStatus { get; init; }

    /// <summary>The instant the rule generated, when this date has since been moved off it.</summary>
    public DateTimeOffset? OriginalStartsAt { get; init; }
}

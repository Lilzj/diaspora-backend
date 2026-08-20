namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Draft an event.
/// <para>
/// Lands as a private draft whatever this says. Publishing is a separate act with its own
/// permission, its own readiness checks and its own spam pass.
/// </para>
/// </summary>
public sealed record CreateEventRequest
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    /// <summary>A key from the event-category catalogue.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>The organizing organization. Optional — most events have none.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Who the event is billed as being by, when that differs from the organizer.</summary>
    public string? HostName { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>
    /// An IANA identifier — <c>Europe/London</c>, <c>Africa/Lagos</c>. Defaults to UTC. What lets a
    /// client render the local time of the place the event is at rather than the reader's.
    /// </summary>
    public string? TimeZoneId { get; init; }

    public DateTimeOffset? RegistrationDeadline { get; init; }

    public DateTimeOffset? CheckInOpensAt { get; init; }

    /// <summary><c>InPerson</c>, <c>Online</c>, <c>Hybrid</c>. Defaults to in person.</summary>
    public string? AttendanceMode { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public EventVenueRequest? Venue { get; init; }

    /// <summary>Null means uncapped, which is right for a service or a street festival.</summary>
    public int? Capacity { get; init; }

    /// <summary>
    /// Whether the event takes responses. Defaults to the category's own setting — a workshop does,
    /// a Sunday service does not.
    /// </summary>
    public bool? RequiresRsvp { get; init; }

    public bool IsFamilyFriendly { get; init; }

    public bool IsChildFriendly { get; init; }

    public bool IsStudentFocused { get; init; }

    /// <summary>
    /// Whether there is anything to pay at the door. A flag, not a price: paid events and ticketing
    /// are explicitly out of scope, and a price field would be the first half of a payment system.
    /// </summary>
    public bool IsFree { get; init; } = true;

    public string? DressCode { get; init; }

    /// <summary>Free text, and required to stay so. An enum would be wrong everywhere.</summary>
    public string? AccessibilityNotes { get; init; }

    public ContactDetailsRequest? Contact { get; init; }

    public IReadOnlyList<MediaAttachmentRequest>? Media { get; init; }

    public IReadOnlyList<Guid>? GuideIds { get; init; }

    public IReadOnlyList<Guid>? ProviderIds { get; init; }
}

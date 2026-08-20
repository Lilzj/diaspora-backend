namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// An event as another module sees it.
/// <para>
/// Flat primitives only. A consumer reads this without referencing a single Community Life type.
/// </para>
/// <para>
/// <b>Carries no attendee list and no attendee count broken down by person.</b> Who is coming to
/// something is the most socially sensitive thing this module holds, and a summary that leaked it
/// to every consumer would defeat the attendee-privacy rule the module enforces everywhere else.
/// </para>
/// </summary>
public sealed record EventSummary
{
    public required Guid EventId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public string? Summary { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>An IANA identifier, so a client renders the local time of the place it is at.</summary>
    public required string TimeZoneId { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? VenueName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>A B5 asset identifier, never a URL.</summary>
    public Guid? CoverMediaId { get; init; }

    public required bool IsFree { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    /// <summary>How many people are coming. A total only — never who.</summary>
    public required int GoingCount { get; init; }

    /// <summary>Places left, or null when the event is uncapped.</summary>
    public int? RemainingPlaces { get; init; }

    /// <summary>The lifecycle state, as its name.</summary>
    public required string Status { get; init; }

    /// <summary>How people attend: <c>InPerson</c>, <c>Online</c>, <c>Hybrid</c>.</summary>
    public required string AttendanceMode { get; init; }
}

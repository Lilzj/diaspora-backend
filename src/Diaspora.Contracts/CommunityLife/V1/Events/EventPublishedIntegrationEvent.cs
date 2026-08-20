using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An event was announced.
/// <para>
/// <b>The module's most consequential contract.</b> The search index, the neighbourhood calendar,
/// the community feed and every discovery surface key on it, which is why it carries a full card
/// payload rather than an identifier.
/// </para>
/// <para>
/// <b>It carries the venue and it carries no attendee.</b> The venue is the invitation — hiding it
/// would defeat the point of announcing an event — and who is coming is the most socially sensitive
/// thing this module holds, so a count travels and a list never does.
/// </para>
/// </summary>
public sealed record EventPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public string? Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>The search facets the category belongs to — religious, cultural, business.</summary>
    public IReadOnlyList<string> Facets { get; init; } = [];

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>An IANA identifier, so a consumer renders the local time of the place it is at.</summary>
    public required string TimeZoneId { get; init; }

    public required string AttendanceMode { get; init; }

    public string? VenueName { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>A B5 asset identifier, never a URL. ADR-005.</summary>
    public Guid? CoverMediaId { get; init; }

    public required bool IsFree { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsChildFriendly { get; init; }

    public required bool IsStudentFocused { get; init; }

    public required bool RequiresRsvp { get; init; }

    public int? Capacity { get; init; }

    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The repeating event this date belongs to — B12.1. Null for a one-off occasion, which is most
    /// of them.
    /// <para>
    /// <b>Appended, never inserted.</b> A record's property order is its serialised order, and a
    /// field placed mid-record shifts every one after it for any consumer reading positionally.
    /// </para>
    /// </summary>
    public Guid? SeriesId { get; init; }

    /// <summary>Its ordinal in that series. Stable: cancelling one date does not renumber the rest.</summary>
    public int? SeriesSequence { get; init; }
}

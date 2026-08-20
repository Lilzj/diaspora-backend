namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An event as a card renders it. What every list screen carries.</summary>
public sealed record EventSummaryResponse
{
    public required Guid EventId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string Summary { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required string TimeZoneId { get; init; }

    public required string AttendanceMode { get; init; }

    public string? VenueName { get; init; }

    public required LocationResponse Location { get; init; }

    public Guid? CoverMediaId { get; init; }

    public required bool IsFree { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    public required int GoingCount { get; init; }

    public int? RemainingPlaces { get; init; }

    public required string Status { get; init; }

    /// <summary>Distance from the search centre, when the query had one.</summary>
    public double? DistanceKm { get; init; }

    public string? ViewerRsvpStatus { get; init; }

    // ── Series membership (B12.1). Appended, never inserted — see EventResponse. ─────────────

    /// <summary>The repeating event this is one date of, when it is one.</summary>
    public Guid? SeriesId { get; init; }

    /// <summary>
    /// Whether the card should say "repeats". Derived from the series reference so a client never
    /// has to compare two fields to reach a rendering decision.
    /// </summary>
    public bool IsRecurring { get; init; }
}

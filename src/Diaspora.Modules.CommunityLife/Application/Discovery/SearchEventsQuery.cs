using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>The event browse screen. Anonymous.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record SearchEventsQuery : IQuery<EventSearchResultResponse>
{
    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public string? Facet { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Only events posted into this Community Programme community — 21 Oct 2026.</summary>
    public Guid? CommunityId { get; init; }

    /// <summary>
    /// Only events posted into the communities the caller belongs to — 4 Nov 2026, the events
    /// screen's "My communities". A signed-out caller belongs to none and gets an empty page.
    /// </summary>
    public bool JoinedCommunitiesOnly { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved and validated by B4.</summary>
    public string? Bounds { get; init; }

    /// <summary>A point to search around (14 Oct 2026), with <see cref="Longitude"/> and <see cref="RadiusKm"/>.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres, or Location's sentinels.</summary>
    public double? RadiusKm { get; init; }

    public DateTimeOffset? StartsAfter { get; init; }

    public DateTimeOffset? StartsBefore { get; init; }

    public bool? FreeOnly { get; init; }

    public bool? FamilyFriendlyOnly { get; init; }

    public bool? ChildFriendlyOnly { get; init; }

    public bool? StudentOnly { get; init; }

    public bool? FeaturedOnly { get; init; }

    public bool? VerifiedOrganizersOnly { get; init; }

    public bool? HasPlacesRemaining { get; init; }

    public string? AttendanceMode { get; init; }

    public bool IncludePast { get; init; }

    public string? SortOrder { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>The event browse screen. Anonymous.</summary>
public sealed record SearchEventsQuery : IQuery<EventSearchResultResponse>
{
    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public string? Facet { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved and validated by B4.</summary>
    public string? Bounds { get; init; }

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

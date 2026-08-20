using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>The organization directory. Anonymous.</summary>
public sealed record SearchOrganizationsQuery : IQuery<OrganizationSearchResultResponse>
{
    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? TypeKeys { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved and validated by B4.</summary>
    public string? Bounds { get; init; }

    public bool? VerifiedOnly { get; init; }

    public bool? FeaturedOnly { get; init; }

    public bool? AcceptingMembers { get; init; }

    public IReadOnlyList<string>? Languages { get; init; }

    public string? SortOrder { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

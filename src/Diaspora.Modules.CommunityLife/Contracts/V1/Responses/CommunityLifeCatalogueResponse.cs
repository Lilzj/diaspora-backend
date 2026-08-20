namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>Both catalogues in one call. What a creation wizard loads before it renders.</summary>
public sealed record CommunityLifeCatalogueResponse
{
    public IReadOnlyList<OrganizationTypeResponse> OrganizationTypes { get; init; } = [];

    public IReadOnlyList<EventCategoryResponse> EventCategories { get; init; } = [];

    /// <summary>The facet names in use across the live categories. What a filter bar renders.</summary>
    public IReadOnlyList<string> Facets { get; init; } = [];
}

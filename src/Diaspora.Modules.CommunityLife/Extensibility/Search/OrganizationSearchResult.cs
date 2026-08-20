namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>What an organization search engine answers with.</summary>
public sealed record OrganizationSearchResult
{
    public IReadOnlyList<OrganizationSearchHit> Hits { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    public int? TotalCount { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>();

    public static OrganizationSearchResult Empty { get; } = new();
}

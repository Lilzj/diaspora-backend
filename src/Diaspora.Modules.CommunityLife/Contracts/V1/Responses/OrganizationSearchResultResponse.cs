namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A page of organizations. See <see cref="EventSearchResultResponse"/> for the engine name.</summary>
public sealed record OrganizationSearchResultResponse
{
    public IReadOnlyList<OrganizationSummaryResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public int? TotalCount { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);

    public string? Engine { get; init; }
}

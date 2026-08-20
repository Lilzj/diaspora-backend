namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>One page of recommendations, featured first.</summary>
public sealed record ProviderRecommendationPageResponse
{
    public required IReadOnlyList<ProviderRecommendationResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required int TotalCount { get; init; }
}

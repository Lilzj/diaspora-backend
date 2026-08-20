namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>One page of reviews, cursor-paged for the same reason providers are.</summary>
public sealed record ProviderReviewPageResponse
{
    public required IReadOnlyList<ProviderReviewResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>The rollup, so a client rendering the first page needs no second call.</summary>
    public ProviderReviewSummaryResponse? Summary { get; init; }
}

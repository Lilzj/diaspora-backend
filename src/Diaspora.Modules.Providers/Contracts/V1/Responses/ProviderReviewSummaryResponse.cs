namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// The rating rollup.
/// <para>
/// The client's <c>rating: { average, totalReviews, distribution }</c> exactly.
/// <see cref="Distribution"/> is keyed by the star as a string because that is how the client
/// declares it, and its <c>RatingDisplay</c> component draws five proportional bars straight
/// from it.
/// </para>
/// </summary>
public sealed record ProviderReviewSummaryResponse
{
    /// <summary>One decimal place, matching every <c>toFixed(1)</c> in the client.</summary>
    public required double Average { get; init; }

    public required int TotalReviews { get; init; }

    /// <summary>Keyed "1" through "5".</summary>
    public required IReadOnlyDictionary<string, int> Distribution { get; init; }
}

namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One member vouching for a provider.
/// <para>
/// No rating, and that is the difference from a review: a recommendation is unambiguously
/// positive, so it can be withdrawn but never scored, and it does not touch the provider's
/// rating rollup.
/// </para>
/// </summary>
public sealed record ProviderRecommendationResponse
{
    public required Guid RecommendationId { get; init; }

    public required Guid ProviderId { get; init; }

    public required Guid RecommenderId { get; init; }

    public string? RecommenderName { get; init; }

    /// <summary>The <c>RecommendationKind</c> name — <c>Recommend</c>, <c>Testimonial</c>, <c>Endorsement</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Required for a testimonial, optional otherwise.</summary>
    public string? Note { get; init; }

    /// <summary>What is being endorsed. "Immigration appeals", "West African catering".</summary>
    public string? Skill { get; init; }

    /// <summary>Whether the provider chose to display this on its profile.</summary>
    public required bool IsFeatured { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

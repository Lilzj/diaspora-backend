namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One plan or promotion suggestion — the client's <c>MonetizationRecommendation</c>, plus what a
/// feedback call needs.
/// <para>
/// <b>Feedback uses the ordinary routes.</b> <c>POST /recommendations/dismiss</c> with
/// <see cref="TargetType"/>, <see cref="TargetId"/>, <see cref="Surface"/>, <see cref="SetId"/> and
/// <see cref="Position"/> suppresses this suggestion like any other card; <see cref="RecommendationId"/>
/// is the same identifier as <see cref="TargetId"/>, as a string.
/// </para>
/// </summary>
public sealed record MonetizationRecommendationResponse
{
    public required string RecommendationId { get; init; }

    /// <summary><c>product</c> or <c>plan</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The product or plan code — what the purchase or plan route takes.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>Why, in a sentence the member can check against their own account.</summary>
    public required string Reason { get; init; }

    /// <summary>Machine-readable reason: <c>subject.unpromoted</c>, <c>product.bought_before</c>,
    /// <c>plan.near_listing_limit</c>, <c>plan.includes_promotions_bought</c>,
    /// <c>plan.matches_provider_role</c>, <c>plan.matches_organization_role</c>.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>The translation key for the generic form of the reason.</summary>
    public required string ReasonResourceKey { get; init; }

    /// <summary>0–1. A strength, never a promise.</summary>
    public required decimal Confidence { get; init; }

    /// <summary>The app route that acts on it.</summary>
    public required string Route { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>The list price, when the catalogue states one.</summary>
    public MonetizationPriceResponse? Price { get; init; }

    /// <summary>Always <c>MonetizationOffer</c>.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Always <c>Monetization</c>.</summary>
    public required string Surface { get; init; }

    /// <summary>The served set, for feedback attribution. Null when sets are not persisted.</summary>
    public Guid? SetId { get; init; }

    public required int Position { get; init; }
}

public sealed record MonetizationPriceResponse
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}

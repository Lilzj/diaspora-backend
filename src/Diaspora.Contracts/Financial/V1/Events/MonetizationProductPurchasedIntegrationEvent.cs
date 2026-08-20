using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Somebody bought something from the catalogue and it is paid for.
/// <para>
/// The list price, the discount and what was paid are all carried, because "what did promotions cost
/// us" is a question no consumer can answer from the paid amount alone.
/// </para>
/// </summary>
public sealed record MonetizationProductPurchasedIntegrationEvent : IntegrationEvent
{
    public required Guid PurchaseId { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required string ProductCode { get; init; }

    /// <summary><c>BoostedListing</c>, <c>FeaturedProvider</c>, <c>CreditBundle</c>, and so on.</summary>
    public required string Category { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required long PaidMinorUnits { get; init; }

    public required long ListMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? PromotionCode { get; init; }

    /// <summary>The policy version that decided the price, when a policy did.</summary>
    public string? PolicyVersion { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset PurchasedAt { get; init; }
}

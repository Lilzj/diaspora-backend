namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One purchase.
/// <para>
/// List price, discount and paid are three separate figures because keeping only the last would make
/// "what did promotions cost us" unanswerable, and recomputing it from today's catalogue would
/// answer it wrongly.
/// </para>
/// </summary>
public sealed record MonetizationPurchaseResponse
{
    public required Guid PurchaseId { get; init; }

    public required string ProductCode { get; init; }

    public required string Category { get; init; }

    public required string Status { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required MoneyResponse ListPrice { get; init; }

    public required MoneyResponse Discount { get; init; }

    public required MoneyResponse Paid { get; init; }

    public string? PromotionCode { get; init; }

    public string? PolicyVersion { get; init; }

    public Guid? PaymentIntentId { get; init; }

    /// <summary>The placement this purchase granted, when it granted one.</summary>
    public PlacementResponse? Placement { get; init; }

    /// <summary>The payment, when the purchase needed one. Carries the next action if any.</summary>
    public PaymentIntentResponse? Payment { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Purchases;

/// <summary>
/// Buys something from the catalogue.
/// <para>
/// <b>The single entry point for every monetizable surface on the platform.</b> A boost on a
/// marketplace listing, a featured provider, a premium property, a highlighted event and a credit
/// bundle all arrive here as a product code and a subject. There is no per-vertical purchase
/// endpoint and no vertical holds a price — which is what "no business module owns payment logic"
/// means in practice rather than in prose.
/// </para>
/// <para>
/// <b>Transactional, and it has to be:</b> a purchase writes the purchase row, a redemption when a
/// coupon applied, a placement, and a payment intent. All four land together or none of them —
/// otherwise a member is charged for a boost that was never created, or gets one they never paid for.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PurchasesCreate)]
[Transactional(Reason =
    "A purchase writes the purchase, a promotion redemption, a placement and a payment intent.")]
[Auditable(Action = "financial.purchase.create")]
public sealed record PurchaseProductCommand : ICommand<MonetizationPurchaseResponse>
{
    public required string ProductCode { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? PromotionCode { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }

    public DateTimeOffset? StartsAt { get; init; }
}

namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Buy something from the catalogue.
/// <para>
/// <b>One request shape for every monetizable surface.</b> A boost on a marketplace listing, a
/// featured provider, a premium property and a credit bundle all arrive here as a product code and a
/// subject; there is no per-vertical purchase endpoint, which is what keeps the verticals free of
/// payment logic.
/// </para>
/// </summary>
public sealed record PurchaseProductRequest
{
    public required string ProductCode { get; init; }

    /// <summary>
    /// What it is for. Omit for a product whose subject is the buyer — a subscription or a credit
    /// bundle — and the platform fills in the caller.
    /// </summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>A coupon to apply. Refused with a specific code when it does not apply.</summary>
    public string? PromotionCode { get; init; }

    /// <summary><c>Card</c>, <c>MobileMoney</c>, <c>Wallet</c>, <c>Credit</c>, and so on.</summary>
    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }

    /// <summary>When the placement should start. Null starts it as soon as the payment captures.</summary>
    public DateTimeOffset? StartsAt { get; init; }
}

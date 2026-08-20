namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A coupon that was applied.
/// <para>
/// <see cref="RemainingUses"/> is null when the promotion is unlimited, which is not the same as
/// zero — the client renders "3 uses left" or nothing at all, never "0 uses left" on a code that
/// still works.
/// </para>
/// </summary>
public sealed record PromotionRedemptionResponse
{
    public required Guid RedemptionId { get; init; }

    public required string Code { get; init; }

    public required MoneyResponse Discount { get; init; }

    public string? ProductCode { get; init; }

    /// <summary>Uses this member has left. Null when unlimited.</summary>
    public int? RemainingUses { get; init; }

    public required DateTimeOffset RedeemedAt { get; init; }
}

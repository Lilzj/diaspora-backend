namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What a code would do, before anybody commits to it.
/// <para>
/// <b>A refused code says which refusal it is.</b> Expired, exhausted, wrong country, wrong product,
/// below the minimum spend, already used — every one of those is a different message and a different
/// action for the member, and a single "invalid code" would be the cheapest possible contract and
/// the most expensive support queue.
/// </para>
/// </summary>
public sealed record PromotionQuoteResponse
{
    public required string Code { get; init; }

    public required bool IsApplicable { get; init; }

    public required MoneyResponse ListPrice { get; init; }

    public required MoneyResponse Discount { get; init; }

    public required MoneyResponse Payable { get; init; }

    /// <summary>A code the client can translate. Null when applicable.</summary>
    public string? RefusalCode { get; init; }
}

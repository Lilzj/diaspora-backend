namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What the platform earned over a period, in one currency.
/// <para>
/// <b>One row per currency, and no all-currency total anywhere.</b> Producing one would need a rate,
/// a rate has a date, and a single figure nobody can reproduce next week is worse than two figures
/// side by side.
/// </para>
/// </summary>
public sealed record RevenueSummaryResponse
{
    public required string Currency { get; init; }

    public required string Dimension { get; init; }

    /// <summary>The country code, module, product code or category. <c>*</c> for a total.</summary>
    public required string DimensionValue { get; init; }

    public required MoneyResponse Gross { get; init; }

    public required MoneyResponse Commission { get; init; }

    public required MoneyResponse Fees { get; init; }

    public required MoneyResponse Refunds { get; init; }

    public required MoneyResponse Net { get; init; }

    public required int TransactionCount { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }
}

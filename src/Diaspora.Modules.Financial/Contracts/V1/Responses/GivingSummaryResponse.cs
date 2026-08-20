namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What has been given, per currency.
/// <para>
/// <see cref="NonMonetaryContributionCount"/> is reported beside the money because on this platform
/// most giving is not money, and a giving summary that counted only cash would understate what the
/// community actually did by a wide margin.
/// </para>
/// </summary>
public sealed record GivingSummaryResponse
{
    public required string Currency { get; init; }

    public required MoneyResponse DonationTotal { get; init; }

    public required int DonationCount { get; init; }

    public required MoneyResponse ContributionTotal { get; init; }

    public required int MonetaryContributionCount { get; init; }

    /// <summary>Goods, volunteer hours, professional skills and supplies. Counted, never valued.</summary>
    public required int NonMonetaryContributionCount { get; init; }
}

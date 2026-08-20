namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// The operator's overview.
/// <para>
/// Every money figure is grouped by currency for the reason stated on the revenue summary: the
/// platform never converts, so a dashboard shows two numbers rather than inventing one.
/// </para>
/// </summary>
public sealed record FinancialDashboardResponse
{
    public required IReadOnlyList<RevenueSummaryResponse> Revenue { get; init; }

    public required int ActiveSubscriptionCount { get; init; }

    public required int TrialSubscriptionCount { get; init; }

    public required int GracePeriodSubscriptionCount { get; init; }

    public required int ActivePlacementCount { get; init; }

    public required int ActiveProductCount { get; init; }

    public required int ActivePolicyCount { get; init; }

    /// <summary>What has been given, per currency. Donations and contributions separately.</summary>
    public required IReadOnlyList<GivingSummaryResponse> Giving { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}

namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What the caller has given.
/// <para>
/// Donations and contributions in one response because the client's giving screen shows them
/// together, and two round trips for one screen is two chances for one of them to be stale.
/// </para>
/// <para>
/// <b>Anonymous gifts are absent from both lists.</b> The identifier was never stored.
/// </para>
/// </summary>
public sealed record GivingHistoryResponse
{
    public required IReadOnlyList<DonationResponse> Donations { get; init; }

    public required IReadOnlyList<ContributionResponse> Contributions { get; init; }

    /// <summary>
    /// How many non-monetary contributions the caller has made — hours, goods, skills. Counted
    /// separately because they have no amount and totalling them with money would be inventing one.
    /// </summary>
    public required int NonMonetaryContributionCount { get; init; }
}

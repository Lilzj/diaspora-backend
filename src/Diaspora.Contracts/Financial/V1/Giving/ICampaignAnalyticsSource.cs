namespace Diaspora.Contracts.Financial.V1.Giving;

/// <summary>
/// One fundraising campaign's giving figures, read by the module that owns campaigns and donations.
/// <para>
/// <b>Why a seam.</b> FRONTEND-PHASE15 asks for a campaign dataset at
/// <c>GET /api/v1/analytics/campaigns/{id}</c>. Analytics references no module and records nothing
/// about a donation. Financial owns the campaign aggregate, the cause it publishes and every gift, and
/// it already decides who may run a campaign — so it answers, and applies its own rule.
/// </para>
/// <para>
/// <b>Money and instants only.</b> No donor identifier crosses this boundary, and no message.
/// </para>
/// </summary>
public interface ICampaignAnalyticsSource
{
    /// <summary>
    /// The figures for one campaign, or null when there is no campaign with this identifier.
    /// <para>
    /// <paramref name="baselineFrom"/> ≤ <paramref name="from"/> ≤ <paramref name="to"/>, all UTC,
    /// each window half-open: the baseline is [<paramref name="baselineFrom"/>, <paramref name="from"/>)
    /// and the period [<paramref name="from"/>, <paramref name="to"/>).
    /// <paramref name="viewerIsPlatformAnalyst"/> is the caller's platform-wide analytics standing,
    /// which the owning module treats as staff standing alongside its own staff rule. When the viewer
    /// may not read the campaign's figures the reading carries none.
    /// </para>
    /// </summary>
    Task<CampaignAnalyticsReading?> ReadAsync(
        Guid campaignId,
        Guid viewerUserId,
        bool viewerIsPlatformAnalyst,
        DateTimeOffset baselineFrom,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

/// <summary>What the owning module reports for one campaign.</summary>
/// <param name="CampaignId">The campaign's identifier.</param>
/// <param name="Title">The campaign's title, for the dataset's label.</param>
/// <param name="ViewerMayRead">
/// Whether the viewer may read the campaign's figures: whoever may run it (its organiser, or for an
/// organisation's campaign anybody holding that body's campaign capability) and staff. False carries
/// no figures.
/// </param>
/// <param name="Status">The campaign's lifecycle state, as the campaign routes name it.</param>
/// <param name="Currency">ISO 4217 code every amount here is in.</param>
/// <param name="Exponent">The currency's minor digits.</param>
/// <param name="CurrencySymbol">The catalogue's symbol, or null when it has none.</param>
/// <param name="TargetMinorUnits">The campaign's target, or null when it set none.</param>
/// <param name="HasCause">
/// Whether the campaign has been published and so has a donation cause. A draft has none and can
/// have received nothing.
/// </param>
/// <param name="RaisedToDateMinorUnits">Completed giving to the cause, all time.</param>
/// <param name="Current">The period's giving.</param>
/// <param name="Baseline">The baseline's giving.</param>
/// <param name="CurrentGifts">
/// Each completed gift in the period: when it completed and how much. For the daily series.
/// </param>
public sealed record CampaignAnalyticsReading(
    Guid CampaignId,
    string Title,
    bool ViewerMayRead,
    string Status,
    string Currency,
    int Exponent,
    string? CurrencySymbol,
    long? TargetMinorUnits,
    bool HasCause,
    long RaisedToDateMinorUnits,
    CampaignGivingWindow Current,
    CampaignGivingWindow Baseline,
    IReadOnlyList<CampaignGift> CurrentGifts);

/// <summary>One window's giving to a campaign's cause.</summary>
/// <param name="CompletedMinorUnits">Completed gifts' total.</param>
/// <param name="CompletedCount">Completed gifts.</param>
/// <param name="SupporterCount">
/// Distinct named donors plus each anonymous gift once — an anonymous gift carries no identifier, so
/// it cannot be told apart from another.
/// </param>
/// <param name="RefundedMinorUnits">Gifts refunded, by amount.</param>
/// <param name="RefundedCount">Gifts refunded.</param>
public sealed record CampaignGivingWindow(
    long CompletedMinorUnits,
    long CompletedCount,
    long SupporterCount,
    long RefundedMinorUnits,
    long RefundedCount)
{
    public static CampaignGivingWindow Empty { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>One completed gift: when it arrived and how much.</summary>
public sealed record CampaignGift(DateTimeOffset CompletedAt, long AmountMinorUnits);

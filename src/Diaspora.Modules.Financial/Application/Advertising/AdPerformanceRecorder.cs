using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>
/// The one door delivery counters are written through.
/// <para>
/// <b>Find-or-open on a unique tuple, and the index behind it is what makes the counters
/// single-valued.</b> Two rows for one campaign, placement, surface and day would each be
/// incremented by whichever the lookup happened to return, and an advertiser's impressions would
/// drift downward with nothing failing anywhere.
/// </para>
/// <para>
/// <b>Nothing here decides what an impression is.</b> B14 does, by marking the items it ranked with
/// its own <c>Promoted</c> reason code; this counts what it was told. A recorder that inferred
/// impressions from a subject's own view counter would be answering a different question — the one
/// MON-GAP-002 says explicitly not to answer.
/// </para>
/// </summary>
public sealed class AdPerformanceRecorder
{
    private readonly IAdPerformanceRepository _performance;
    private readonly IGuidGenerator _guids;

    public AdPerformanceRecorder(IAdPerformanceRepository performance, IGuidGenerator guids)
    {
        _performance = performance;
        _guids = guids;
    }

    /// <summary>
    /// Records served impressions for a campaign.
    /// <para>
    /// "Served", not "seen": an item served at the bottom of a list nobody scrolled to is counted,
    /// because the platform cannot observe whether it entered a viewport and will not guess.
    /// </para>
    /// </summary>
    public Task RecordImpressionsAsync(
        AdCampaign campaign,
        PlacementId? placementId,
        string? surface,
        long count,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            campaign, placementId, surface, occurredAt,
            row => row.AddImpressions(count), cancellationToken);

    /// <summary>
    /// Records a click through to the subject.
    /// <para>
    /// Reported by the device, because only the device knows a tap. The caller is responsible for
    /// checking the campaign is delivering before calling — a click against a paused campaign is a
    /// number an advertiser would be shown for delivery they did not buy.
    /// </para>
    /// </summary>
    public Task RecordClickAsync(
        AdCampaign campaign,
        PlacementId? placementId,
        string? surface,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            campaign, placementId, surface, occurredAt,
            row => row.AddClicks(1), cancellationToken);

    /// <summary>Records the outcome the objective names. Nothing writes these yet.</summary>
    public Task RecordConversionAsync(
        AdCampaign campaign,
        PlacementId? placementId,
        string? surface,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            campaign, placementId, surface, occurredAt,
            row => row.AddConversions(1), cancellationToken);

    /// <summary>
    /// Records what the campaign was charged for a day's delivery.
    /// <para>
    /// <b>Written beside the campaign's own spend, never derived from the counts here.</b> The
    /// campaign is the authority on how much was taken; this row is the authority on which day it
    /// went. Deriving one from the other would give an advertiser two numbers that could disagree.
    /// </para>
    /// </summary>
    public Task RecordSpendAsync(
        AdCampaign campaign,
        PlacementId? placementId,
        string? surface,
        long minorUnits,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            campaign, placementId, surface, occurredAt,
            row => row.AddSpend(minorUnits), cancellationToken);

    private async Task ApplyAsync(
        AdCampaign campaign,
        PlacementId? placementId,
        string? surface,
        DateTimeOffset occurredAt,
        Action<AdPerformanceDay> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        // Bucketed in UTC so a campaign spanning zones has one calendar, and so two replicas in
        // two regions agree about which day a delivery belongs to.
        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        var name = string.IsNullOrWhiteSpace(surface) ? "Unknown" : surface.Trim();

        var row = await _performance.FindAsync(
            campaign.Id, placementId, name, day, cancellationToken);

        if (row is null)
        {
            row = AdPerformanceDay.Open(
                AdPerformanceDayId.From(_guids.NewSequentialGuid()),
                campaign.Id,
                placementId,
                name,
                day,
                campaign.Currency,
                campaign.Exponent);

            _performance.Add(row);
        }

        change(row);
    }
}

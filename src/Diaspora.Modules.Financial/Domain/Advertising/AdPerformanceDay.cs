using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// One campaign's delivery on one day, on one surface.
/// <para>
/// <b>Counters, not events, and the choice is about privacy before it is about size.</b> A row per
/// impression would mean a table that records, for ever, that a particular person was shown a
/// particular advertisement — on a platform whose members' communities can disclose nationality,
/// immigration status, religion and health. These rows hold no viewer identity at all. What an
/// advertiser is owed is how many, and how many is what is stored.
/// </para>
/// <para>
/// It is also the only shape that answers the dashboard without a scan: a campaign's whole
/// performance is a sum over at most one row per surface per day it ran.
/// </para>
///
/// <para>
/// <b>The metric definitions, which MON-GAP-002 asks for by name.</b> §20 forbids inventing metric
/// semantics, so each of these says exactly what it counts and — as importantly — what it does not:
/// </para>
/// <list type="bullet">
/// <item><see cref="Impressions"/> — the number of times the platform <em>served</em> this subject
/// in a ranked position it held <em>because</em> it was promoted. B14 marks those items with its
/// own <c>Promoted</c> reason code and reports them; nothing here is inferred from a subject's own
/// view counter, which would answer a different question. <b>Served is not seen:</b> an item served
/// at the bottom of a list nobody scrolled to is counted, because the platform cannot observe
/// whether it entered a viewport and will not guess.</item>
/// <item><see cref="Clicks"/> — the number of times somebody opened the subject from a promoted
/// position. Reported by the device, because only the device knows a tap, and accepted only against
/// a campaign that is currently delivering.</item>
/// <item><see cref="Conversions"/> — the number of times the subject's own vertical recorded the
/// outcome the objective names. Nothing writes these yet; the field exists because the objective
/// does, and it reads as zero rather than as absent.</item>
/// <item><see cref="SpendMinorUnits"/> — what the campaign was charged for this day's delivery.
/// Written by the same path that moves the campaign's spend, never derived from the counts here.</item>
/// </list>
/// <para>
/// <b>Deduplication: none, and it is stated rather than implied.</b> Two impressions of the same
/// campaign to the same person an hour apart count twice. Deduplicating would require keeping who
/// saw what, which is the thing this design refuses to keep.
/// </para>
/// <para>
/// <b>Reporting lag: one integration event.</b> Impressions arrive through the outbox rather than
/// on the read path, so a campaign's figures trail delivery by however long the dispatcher takes —
/// seconds under load, and never instant.
/// </para>
/// </summary>
public sealed class AdPerformanceDay : Entity<AdPerformanceDayId>
{
    private AdPerformanceDay(
        AdPerformanceDayId id,
        AdCampaignId campaignId,
        PlacementId? placementId,
        string surface,
        DateOnly day,
        string currency,
        int exponent)
        : base(id)
    {
        CampaignId = campaignId;
        PlacementId = placementId;
        Surface = surface;
        Day = day;
        Currency = currency;
        Exponent = exponent;
    }

    // EF Core.
    private AdPerformanceDay() { }

    public AdCampaignId CampaignId { get; private set; } = default!;

    /// <summary>
    /// The placement this delivery was booked under, where one is known.
    /// <para>
    /// Null when the campaign was served without the platform being able to say which of its
    /// placements carried it. A campaign-level figure is still true; attributing it to a placement
    /// that may not have been the one is not.
    /// </para>
    /// </summary>
    public PlacementId? PlacementId { get; private set; }

    /// <summary>Where it was served — B14's surface name. Kept as text; B15 does not own the list.</summary>
    public string Surface { get; private set; } = string.Empty;

    /// <summary>The UTC day. Bucketed in UTC so a campaign spanning zones has one calendar.</summary>
    public DateOnly Day { get; private set; }

    public long Impressions { get; private set; }

    public long Clicks { get; private set; }

    public long Conversions { get; private set; }

    public long SpendMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; }

    public static AdPerformanceDay Open(
        AdPerformanceDayId id,
        AdCampaignId campaignId,
        PlacementId? placementId,
        string? surface,
        DateOnly day,
        string currency,
        int exponent)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(campaignId);

        var name = surface?.Trim();

        return new AdPerformanceDay(
            id,
            campaignId,
            placementId,
            string.IsNullOrEmpty(name) ? "Unknown" : name,
            day,
            currency,
            exponent);
    }

    /// <summary>Adds served impressions. See the type summary for what "served" means here.</summary>
    public void AddImpressions(long count) => Impressions += Math.Max(0, count);

    public void AddClicks(long count) => Clicks += Math.Max(0, count);

    public void AddConversions(long count) => Conversions += Math.Max(0, count);

    /// <summary>Adds what the campaign was charged for this day. Never derived from the counts.</summary>
    public void AddSpend(long minorUnits) => SpendMinorUnits += Math.Max(0, minorUnits);
}

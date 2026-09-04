using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>
/// Turns delivery counters into what the dashboard draws.
/// <para>
/// <b>Every derived figure is computed here and none on the device.</b> Cost per result is a
/// division that has to agree with the spend the campaign was actually charged, and a client doing
/// it from two rounded numbers would show a figure the platform never calculated.
/// </para>
/// <para>
/// <b>The metric definitions travel with the numbers.</b> MON-GAP-002 asks for "a documented
/// definition for every metric — what counts as an impression, what deduplication is applied, what
/// the reporting lag is", and a definition that lives only in a comment is one the person reading
/// the dashboard never sees.
/// </para>
/// </summary>
internal static class AdPerformanceMapper
{
    public static AdPerformanceResponse ToResponse(
        IReadOnlyList<AdPerformanceDay> rows,
        DateOnly from,
        DateOnly to,
        AdCampaign? campaign)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var impressions = rows.Sum(row => row.Impressions);
        var clicks = rows.Sum(row => row.Clicks);
        var conversions = rows.Sum(row => row.Conversions);
        var spend = rows.Sum(row => row.SpendMinorUnits);

        // The campaign is the authority on currency. Without one — a placement or a rollup — take
        // it from the rows, and fall back to the platform's own default only when there are none.
        var first = rows.Count > 0 ? rows[0] : null;

        var currency = campaign?.Currency ?? first?.Currency ?? "GBP";
        var exponent = campaign?.Exponent ?? first?.Exponent ?? 2;

        // What the objective counts as a result. Awareness is paid for by being seen; everything
        // else is paid for by somebody doing something.
        var results = campaign?.Objective switch
        {
            AdCampaignObjective.Awareness => impressions,
            AdCampaignObjective.Traffic => clicks,
            AdCampaignObjective.Engagement => clicks,
            AdCampaignObjective.Leads => conversions,
            _ => clicks
        };

        return new AdPerformanceResponse
        {
            From = from,
            To = to,

            Impressions = impressions,
            Clicks = clicks,
            Conversions = conversions,

            // Null rather than zero when nothing was served. A click-through rate of 0% and "we
            // have not shown this to anybody yet" are different sentences, and only one of them is
            // a reason to change the creative.
            ClickThroughRate = impressions > 0
                ? Math.Round((decimal)clicks / impressions, 6)
                : null,

            Spend = new AdMoneyResponse
            {
                MinorUnits = spend,
                Currency = currency,
                Exponent = exponent
            },

            Results = results,

            CostPerResult = results > 0
                ? new AdMoneyResponse
                {
                    MinorUnits = spend / results,
                    Currency = currency,
                    Exponent = exponent
                }
                : null,

            BudgetConsumedFraction = campaign is { BudgetMinorUnits: > 0 }
                ? Math.Round((decimal)campaign.SpendMinorUnits / campaign.BudgetMinorUnits, 4)
                : null,

            Daily = [.. rows
                .GroupBy(row => row.Day)
                .OrderBy(group => group.Key)
                .Select(group => new AdPerformanceDayResponse
                {
                    Day = group.Key,
                    Impressions = group.Sum(row => row.Impressions),
                    Clicks = group.Sum(row => row.Clicks),
                    Conversions = group.Sum(row => row.Conversions),
                    SpendMinorUnits = group.Sum(row => row.SpendMinorUnits)
                })],

            BySurface = [.. rows
                .GroupBy(row => row.Surface, StringComparer.Ordinal)
                .OrderByDescending(group => group.Sum(row => row.Impressions))
                .Select(group => new AdPerformanceSurfaceResponse
                {
                    Surface = group.Key,
                    Impressions = group.Sum(row => row.Impressions),
                    Clicks = group.Sum(row => row.Clicks)
                })],

            Definitions = Definitions
        };
    }

    /// <summary>
    /// What each number means, sent with every response.
    /// <para>
    /// Static because they are properties of the platform rather than of a campaign, and on the
    /// wire because MON-GAP-002 asks for them and because §20 forbids inventing metric semantics —
    /// which includes letting a client invent them by guessing.
    /// </para>
    /// </summary>
    private static readonly AdMetricDefinitionsResponse Definitions = new()
    {
        Impression =
            "The number of times Diaspora showed this in a position it held because it was "
            + "promoted. It counts what was shown, not what was looked at: something shown at the "
            + "bottom of a list nobody scrolled to is still counted, because we cannot tell.",

        Click =
            "The number of times somebody opened this from a promoted position. Reported by the "
            + "app, and only counted while the campaign is running.",

        Conversion =
            "The number of times the outcome your objective names was recorded. Nothing records "
            + "these yet, so this reads as zero.",

        Result =
            "What your objective is paid for. Awareness counts impressions; Traffic and Engagement "
            + "count clicks; Leads counts conversions.",

        Deduplication =
            "None. Showing the same campaign to the same person twice counts twice — telling them "
            + "apart would mean keeping a record of who saw what, which Diaspora does not keep.",

        ReportingLag =
            "Figures follow delivery rather than arriving with it, usually within seconds. A "
            + "campaign that has just started may show nothing for a short while."
    };
}

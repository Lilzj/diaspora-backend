using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Extensibility.Search;

/// <summary>
/// The rent range present in a result set, for one currency and one rent period.
/// <para>
/// <b>Scoped to a pair, because there is no single range.</b> A result set holding a nightly
/// hostel bed at 20 USD and a monthly flat at 1,200 GBP has no meaningful minimum or maximum
/// between them, and a slider drawn across both would be a control nobody could use. The facet is
/// keyed by currency and period so a client renders the slider for whichever pair the member is
/// actually filtering in.
/// </para>
/// </summary>
public sealed record HousingPriceRangeFacet
{
    public required string CurrencyCode { get; init; }

    public required RentPeriod Period { get; init; }

    public required decimal Minimum { get; init; }

    public required decimal Maximum { get; init; }

    /// <summary>How many properties quote in this currency and period.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// The key this facet appears under, and the one a caller reconstructs to look it up.
    /// Currency and period joined, because both are needed to identify the range.
    /// </summary>
    public static string KeyFor(string currencyCode, RentPeriod period) =>
        $"{currencyCode}:{period}";
}

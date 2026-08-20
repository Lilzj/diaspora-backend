using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Analytics;

/// <summary>
/// One let, reduced to what a market statistic is computed from.
/// <para>
/// <b>A projection rather than an aggregate, and the grouping keys are on it for a reason.</b>
/// Currency and rent period are carried on every row because an average rent is only meaningful
/// within one of each: mixing a nightly hostel bed into a monthly flat average, or rupees into
/// pounds, produces a number that is wrong in a way nobody can see. The caller groups first and
/// averages second, and this shape is what forces that order.
/// </para>
/// </summary>
public sealed record HousingMarketRow
{
    public required Guid? NeighborhoodAreaId { get; init; }

    public required string CountryCode { get; init; }

    public required string AccommodationTypeKey { get; init; }

    /// <summary>What it let for. Null when the listing never disclosed a rent.</summary>
    public decimal? Amount { get; init; }

    /// <summary>A grouping key, never something to convert across.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>A grouping key. A nightly rate is not a monthly rent divided by thirty.</summary>
    public RentPeriod Period { get; init; }

    /// <summary>Publication to occupation. Null for a property that has not let.</summary>
    public TimeSpan? TimeToLet { get; init; }

    public required bool HasLet { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}

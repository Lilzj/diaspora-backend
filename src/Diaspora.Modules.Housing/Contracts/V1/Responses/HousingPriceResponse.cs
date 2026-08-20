namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What it costs to live there.
/// <para>
/// The currency and the period always travel with the amount. A bare number on a multi-country
/// platform is a number a member will read in their own currency and at their own billing cycle,
/// and nothing here converts either.
/// </para>
/// </summary>
public sealed record HousingPriceResponse
{
    /// <summary>False for price-on-application, which is a real state rather than a zero rent.</summary>
    public required bool IsDisclosed { get; init; }

    public decimal? Amount { get; init; }

    public string? CurrencyCode { get; init; }

    /// <summary>PerNight, PerWeek, PerMonth, PerQuarter, PerYear or PerStay.</summary>
    public string? Period { get; init; }

    public decimal? DepositAmount { get; init; }

    public required bool DepositRequired { get; init; }

    /// <summary>
    /// Rent plus deposit, where both are known. What a member is actually asked for on day one, and
    /// the figure no listing states plainly.
    /// </summary>
    public decimal? MoveInTotal { get; init; }

    public required bool IsNegotiable { get; init; }

    public required bool UtilitiesIncluded { get; init; }

    public required bool InternetIncluded { get; init; }
}

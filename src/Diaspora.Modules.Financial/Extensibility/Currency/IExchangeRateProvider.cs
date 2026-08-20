namespace Diaspora.Modules.Financial.Extensibility.Currency;

/// <summary>
/// Where a rate would come from, if the platform converted. It does not.
/// <para>
/// <b>Declared with no implementation, deliberately, and this is not an oversight.</b> Every stored
/// amount on this platform is in the currency it was denominated in, every response carries that
/// currency, and no surface totals across currencies. A dashboard showing GBP and NGN side by side
/// is honest; one showing a single figure would be a number nobody could reproduce next week.
/// </para>
/// <para>
/// <b>What this seam buys is that conversion, when it arrives, arrives in one place.</b> Rates have
/// spreads, dates, sources and rounding rules, and a platform that scattered <c>* rate</c> through
/// its handlers would have to find every one of them again. Any surface that would need a rate
/// returns <c>financial.conversion.not_supported</c> today.
/// </para>
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>Null when no rate is available. Never a guess and never 1.0.</summary>
    Task<ExchangeRate?> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}

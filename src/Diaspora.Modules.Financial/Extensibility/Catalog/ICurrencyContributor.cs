namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>
/// A module declaring currencies.
/// <para>
/// The Financial Platform ships the ones the client already names — GBP, USD, EUR, NGN, GHS, MUR,
/// KES, ZAR, CAD, AED — and a deployment adds its own. The exponent is the field that matters: a
/// platform assuming two decimal places would be wrong about a quarter of the world's currencies,
/// silently, by a factor of a hundred.
/// </para>
/// </summary>
public interface ICurrencyContributor
{
    string Module { get; }

    IEnumerable<CurrencySeed> GetCurrencies();
}

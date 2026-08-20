using Diaspora.Modules.Financial.Extensibility.Catalog;

namespace Diaspora.Modules.Financial.Catalog;

/// <summary>
/// The currencies the platform ships knowing about.
/// <para>
/// Every one the React Native client already names in a mock, plus the two the diaspora corridors
/// this platform serves most obviously need. A deployment adds its own by contributing more; nothing
/// here is exhaustive and nothing in the module switches on a code.
/// </para>
/// <para>
/// <b>Every one of these has an exponent of 2, and that is a fact about this list rather than about
/// currencies.</b> The column exists because the next contributor may well add JPY, and a platform
/// that had assumed two decimals would be wrong by a factor of a hundred, silently, in the direction
/// of charging too much.
/// </para>
/// </summary>
public sealed class DefaultCurrencyContributor : ICurrencyContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<CurrencySeed> GetCurrencies()
    {
        yield return new CurrencySeed
        {
            Code = "GBP", Name = "Pound Sterling", Symbol = "£", SortOrder = 10
        };

        yield return new CurrencySeed
        {
            Code = "USD", Name = "US Dollar", Symbol = "$", SortOrder = 20
        };

        yield return new CurrencySeed
        {
            Code = "EUR", Name = "Euro", Symbol = "€", SortOrder = 30
        };

        yield return new CurrencySeed
        {
            Code = "NGN", Name = "Nigerian Naira", Symbol = "₦", SortOrder = 40
        };

        yield return new CurrencySeed
        {
            Code = "GHS", Name = "Ghanaian Cedi", Symbol = "₵", SortOrder = 50
        };

        yield return new CurrencySeed
        {
            Code = "KES", Name = "Kenyan Shilling", Symbol = "KSh", SortOrder = 60
        };

        yield return new CurrencySeed
        {
            Code = "ZAR", Name = "South African Rand", Symbol = "R", SortOrder = 70
        };

        yield return new CurrencySeed
        {
            Code = "MUR", Name = "Mauritian Rupee", Symbol = "Rs", SortOrder = 80
        };

        yield return new CurrencySeed
        {
            Code = "CAD", Name = "Canadian Dollar", Symbol = "C$", SortOrder = 90
        };

        yield return new CurrencySeed
        {
            Code = "AED", Name = "UAE Dirham", Symbol = "د.إ", SortOrder = 100
        };
    }
}

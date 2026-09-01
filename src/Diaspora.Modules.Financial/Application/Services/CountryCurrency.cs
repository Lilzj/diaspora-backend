namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// The currency a member's country of residence is denominated in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <see cref="FinancialIdentityService.GetOrCreateAsync"/> was handed the
/// member's <c>countryOfResidence</c> and a null currency, stored the country on the account, and
/// then resolved the currency as <c>NormalizeCurrency(currency) ?? DefaultCurrency</c> — which
/// falls back to <c>"GBP"</c>. The country was one line above the decision that ignored it.
/// </para>
/// <para>
/// The result was that <b>every member of the platform was given a Pound Sterling wallet</b>,
/// including members resident in Mauritius whose own marketplace listings were priced in MUR.
/// Confirmed on a release build: the Wallet screen opened on "£0.00 / GBP" for a Mauritian member
/// (UAT-124). Deposits and withdrawals are disabled today, which is the only reason that was a
/// display defect rather than a settlement one — <c>Money.cs</c> states the platform
/// <b>never converts</b>, so the day money moves a wrong wallet currency stops being cosmetic.
/// </para>
/// <para>
/// <b>Why the mapping lives here and not in Location.</b> <c>location.countries.currency_code</c>
/// already carries this fact, and Location owns it. Financial reaching into another module's
/// tables to open an account would couple the two in the one direction that is hardest to unpick —
/// account opening happens on a domain event, outside any request that could carry a Location
/// lookup. This is the small, explicit subset Financial needs, and the test beside it asserts the
/// two agree so the duplicate cannot drift silently.
/// </para>
/// <para>
/// <b>Every entry is checked against the live currency catalogue before use.</b> Mapping a country
/// to a currency the deployment has not activated would open a wallet in a currency nothing can
/// price, which is worse than the wrong-but-working currency this replaces.
/// </para>
/// </remarks>
public static class CountryCurrency
{
    /// <summary>
    /// ISO 3166-1 alpha-2 to ISO 4217, for the countries this platform serves.
    /// </summary>
    /// <remarks>
    /// Deliberately not a complete world map. An entry here is a claim that the deployment can
    /// hold and price money in that currency; a country that is absent falls back to the
    /// configured default, which is the honest answer for a market the platform has not opened.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> ByCountry =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // The platform's supported and featured countries.
            ["MU"] = "MUR",
            ["GB"] = "GBP",
            ["NG"] = "NGN",
            ["GH"] = "GHS",
            ["KE"] = "KES",
            ["ZA"] = "ZAR",

            // Corridors the currency catalogue already carries.
            ["CA"] = "CAD",
            ["AE"] = "AED",
            ["US"] = "USD",

            // The euro members among the diaspora destinations this product names.
            ["IE"] = "EUR",
            ["FR"] = "EUR",
            ["DE"] = "EUR",
            ["NL"] = "EUR",
            ["BE"] = "EUR",
            ["IT"] = "EUR",
            ["ES"] = "EUR",
            ["PT"] = "EUR",
        };

    /// <summary>
    /// The currency for a country, or <c>null</c> when this platform has no opinion.
    /// </summary>
    /// <remarks>
    /// <c>null</c> is a real answer and callers must keep it: it means "fall back to the
    /// configured default", not "assume sterling". Returning a guess for an unserved market would
    /// reintroduce the defect this type exists to remove, just with a different wrong currency.
    /// </remarks>
    public static string? For(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        return ByCountry.TryGetValue(countryCode.Trim(), out var currency) ? currency : null;
    }

    /// <summary>Every country this map has an opinion about. For the drift test.</summary>
    public static IReadOnlyCollection<string> KnownCountries => (IReadOnlyCollection<string>)ByCountry.Keys;

    /// <summary>Every currency this map can produce. For the drift test.</summary>
    public static IReadOnlyCollection<string> KnownCurrencies => ByCountry.Values.Distinct().ToArray();
}

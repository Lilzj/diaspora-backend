using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// The shipped country metadata: calling code for every country, and currency, language and
/// timezone for the markets the platform actually runs in.
/// <para>
/// Deliberately partial. Calling codes are cheap and complete because the frontend already
/// carries them; currency, language and timezone are supplied for the launch markets and the
/// major diaspora corridors, and left null everywhere else. A table of 195 currencies in this
/// file would be wrong within a year and wrong in a way nobody notices — currencies get
/// redenominated, and a stale one silently misprices a listing.
/// </para>
/// <para>
/// Order zero, so a deployment with a real reference dataset registers a contributor above
/// this one and overrides it without editing anything here.
/// </para>
/// </summary>
public sealed class DefaultCountryMetadataContributor : ICountryMetadataContributor
{
    /// <summary>
    /// Currency, primary language and primary timezone for the markets and corridors that
    /// matter at launch. Everything else gets a calling code and nothing more.
    /// </summary>
    private static readonly Dictionary<string, MarketProfile> Markets =
        new Dictionary<string, MarketProfile>(StringComparer.Ordinal)
        {
            ["GB"] = new("GBP", "en", "Europe/London", "Europe", "Northern Europe"),
            ["MU"] = new("MUR", "en", "Indian/Mauritius", "Africa", "Eastern Africa"),
            ["NG"] = new("NGN", "en", "Africa/Lagos", "Africa", "Western Africa"),
            ["GH"] = new("GHS", "en", "Africa/Accra", "Africa", "Western Africa"),
            ["KE"] = new("KES", "en", "Africa/Nairobi", "Africa", "Eastern Africa"),
            ["ZA"] = new("ZAR", "en", "Africa/Johannesburg", "Africa", "Southern Africa"),
            ["US"] = new("USD", "en", "America/New_York", "Americas", "Northern America"),
            ["CA"] = new("CAD", "en", "America/Toronto", "Americas", "Northern America"),
            ["FR"] = new("EUR", "fr", "Europe/Paris", "Europe", "Western Europe"),
            ["DE"] = new("EUR", "de", "Europe/Berlin", "Europe", "Western Europe"),
            ["IE"] = new("EUR", "en", "Europe/Dublin", "Europe", "Northern Europe"),
            ["NL"] = new("EUR", "nl", "Europe/Amsterdam", "Europe", "Western Europe"),
            ["ES"] = new("EUR", "es", "Europe/Madrid", "Europe", "Southern Europe"),
            ["IT"] = new("EUR", "it", "Europe/Rome", "Europe", "Southern Europe"),
            ["PT"] = new("EUR", "pt", "Europe/Lisbon", "Europe", "Southern Europe"),
            ["AU"] = new("AUD", "en", "Australia/Sydney", "Oceania", "Australia and New Zealand"),
            ["NZ"] = new("NZD", "en", "Pacific/Auckland", "Oceania", "Australia and New Zealand"),
            ["AE"] = new("AED", "ar", "Asia/Dubai", "Asia", "Western Asia"),
            ["IN"] = new("INR", "en", "Asia/Kolkata", "Asia", "Southern Asia"),
            ["CI"] = new("XOF", "fr", "Africa/Abidjan", "Africa", "Western Africa"),
            ["SN"] = new("XOF", "fr", "Africa/Dakar", "Africa", "Western Africa"),
            ["CM"] = new("XAF", "fr", "Africa/Douala", "Africa", "Middle Africa"),
            ["ET"] = new("ETB", "am", "Africa/Addis_Ababa", "Africa", "Eastern Africa"),
            ["TZ"] = new("TZS", "sw", "Africa/Dar_es_Salaam", "Africa", "Eastern Africa"),
            ["UG"] = new("UGX", "en", "Africa/Kampala", "Africa", "Eastern Africa"),
            ["ZW"] = new("ZWL", "en", "Africa/Harare", "Africa", "Eastern Africa"),
            ["JM"] = new("JMD", "en", "America/Jamaica", "Americas", "Caribbean"),
            ["TT"] = new("TTD", "en", "America/Port_of_Spain", "Americas", "Caribbean"),
            ["BR"] = new("BRL", "pt", "America/Sao_Paulo", "Americas", "South America"),
            ["PH"] = new("PHP", "en", "Asia/Manila", "Asia", "South-eastern Asia")
        };

    private static readonly Dictionary<string, string> CallingCodes =
        WorldCountryData.All.ToDictionary(
            record => record.Code, record => record.CallingCode, StringComparer.Ordinal);

    public int Order => 0;

    public CountryMetadata? MetadataFor(CountryCode countryCode)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        var hasCallingCode = CallingCodes.TryGetValue(countryCode.Value, out var callingCode);
        var hasMarket = Markets.TryGetValue(countryCode.Value, out var market);

        // Null rather than an empty record: an empty record overrides, and a contributor that
        // meant "no opinion" would erase whatever a lower-ordered one had supplied.
        if (!hasCallingCode && !hasMarket)
        {
            return null;
        }

        return CountryMetadata.Create(
            callingCode: hasCallingCode ? TryCalling(callingCode!) : null,
            currency: hasMarket ? TryCurrency(market!.Currency) : null,
            primaryLanguage: hasMarket ? TryLanguage(market!.Language) : null,
            defaultLocale: null,
            primaryTimeZone: hasMarket ? TryTimeZone(market!.TimeZone) : null,
            continent: market?.Continent,
            subregion: market?.Subregion);
    }

    private static CallingCode? TryCalling(string value) =>
        Domain.ValueObjects.CallingCode.TryCreate(value) is { IsSuccess: true } result
            ? result.Value
            : null;

    private static CurrencyCode? TryCurrency(string value) =>
        Domain.ValueObjects.CurrencyCode.TryCreate(value) is { IsSuccess: true } result
            ? result.Value
            : null;

    private static LanguageCode? TryLanguage(string value) =>
        Domain.ValueObjects.LanguageCode.TryCreate(value) is { IsSuccess: true } result
            ? result.Value
            : null;

    private static TimeZoneReference? TryTimeZone(string value) =>
        TimeZoneReference.TryCreate(value) is { IsSuccess: true } result ? result.Value : null;

    private sealed record MarketProfile(
        string Currency, string Language, string TimeZone, string Continent, string Subregion);
}

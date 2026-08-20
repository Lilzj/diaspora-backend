using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Countries;

/// <summary>
/// The facts about a country that other modules keep re-deriving badly: what it dials, what
/// it spends, what it speaks, and what time it is there.
/// <para>
/// Every one of these is currently guessed somewhere in the platform. Community Life asks
/// its create form to type a timezone; Marketplace, Housing and Jobs each carry a free-text
/// <c>currency</c> with a comment listing examples; the phone input reads a calling code from
/// a list bundled into the app. One authoritative record ends four independent sources of
/// the same truth.
/// </para>
/// <para>
/// Every field is optional. A country the platform knows exists but has no metadata for is a
/// normal state during a partial import, and a metadata record that could not be constructed
/// without a currency would mean the country could not be listed at all.
/// </para>
/// </summary>
public sealed class CountryMetadata : ValueObject
{
    public CallingCode? CallingCode { get; }

    public CurrencyCode? Currency { get; }

    /// <summary>The most widely used official language.</summary>
    public LanguageCode? PrimaryLanguage { get; }

    /// <summary>The locale a client should default to before the member expresses a preference.</summary>
    public LocaleCode? DefaultLocale { get; }

    /// <summary>The zone the capital keeps. Large countries have others; this is the default.</summary>
    public TimeZoneReference? PrimaryTimeZone { get; }

    /// <summary>UN geoscheme continent — "Europe", "Africa". Used for grouping country lists.</summary>
    public string? Continent { get; }

    /// <summary>UN geoscheme subregion — "Western Europe", "Eastern Africa".</summary>
    public string? Subregion { get; }

    private CountryMetadata(
        CallingCode? callingCode,
        CurrencyCode? currency,
        LanguageCode? primaryLanguage,
        LocaleCode? defaultLocale,
        TimeZoneReference? primaryTimeZone,
        string? continent,
        string? subregion)
    {
        CallingCode = callingCode;
        Currency = currency;
        PrimaryLanguage = primaryLanguage;
        DefaultLocale = defaultLocale;
        PrimaryTimeZone = primaryTimeZone;
        Continent = continent;
        Subregion = subregion;
    }

    /// <summary>A country the platform lists but knows nothing else about yet.</summary>
    public static CountryMetadata Empty => new(null, null, null, null, null, null, null);

    public static CountryMetadata Create(
        CallingCode? callingCode = null,
        CurrencyCode? currency = null,
        LanguageCode? primaryLanguage = null,
        LocaleCode? defaultLocale = null,
        TimeZoneReference? primaryTimeZone = null,
        string? continent = null,
        string? subregion = null) =>
        new(callingCode,
            currency,
            primaryLanguage,
            // A country with a language and no explicit locale has an obvious one. Deriving it
            // beats storing a second copy that can disagree with the first.
            defaultLocale,
            primaryTimeZone,
            Trim(continent),
            Trim(subregion));

    /// <summary>
    /// This metadata with a locale filled in from the primary language when one was not
    /// supplied. Kept separate from <see cref="Create"/> so the country's own code can pass
    /// itself in without the value object needing to know it.
    /// </summary>
    public CountryMetadata WithDerivedLocale(CountryCode country)
    {
        ArgumentNullException.ThrowIfNull(country);

        if (DefaultLocale is not null || PrimaryLanguage is null)
        {
            return this;
        }

        return new CountryMetadata(
            CallingCode,
            Currency,
            PrimaryLanguage,
            LocaleCode.Create(PrimaryLanguage, country),
            PrimaryTimeZone,
            Continent,
            Subregion);
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CallingCode;
        yield return Currency;
        yield return PrimaryLanguage;
        yield return DefaultLocale;
        yield return PrimaryTimeZone;
        yield return Continent;
        yield return Subregion;
    }
}

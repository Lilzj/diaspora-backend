namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The facts about a country that other modules currently each guess separately.
/// <para>
/// Every field is nullable. A country in the catalogue with no metadata yet is a normal state
/// during an import, and a client that assumed otherwise would be surprised in production
/// rather than in review.
/// </para>
/// </summary>
public sealed record CountryMetadataResponse
{
    /// <summary>E.164, with the leading plus.</summary>
    public string? CallingCode { get; init; }

    /// <summary>ISO 4217. The authoritative default for every price in this country.</summary>
    public string? Currency { get; init; }

    /// <summary>ISO 639-1.</summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>BCP 47, e.g. <c>en-GB</c>.</summary>
    public string? DefaultLocale { get; init; }

    /// <summary>IANA, e.g. <c>Europe/London</c>. Not a UTC offset — offsets are wrong for
    /// half the year wherever daylight saving applies.</summary>
    public string? PrimaryTimeZone { get; init; }

    public string? Continent { get; init; }

    public string? Subregion { get; init; }

    /// <summary>Every language with official or widely spoken status.</summary>
    public required IReadOnlyList<string> SpokenLanguages { get; init; }

    /// <summary>Every IANA zone observed. Ordered; the first is the primary.</summary>
    public required IReadOnlyList<string> TimeZones { get; init; }
}

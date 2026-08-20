using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// Where and in what language a signal happened.
/// <para>
/// <b>Captured at the moment the signal is recorded rather than read back from the profile at
/// ranking time, and the difference is a member who moves.</b> Somebody who spent a year in London
/// and now lives in Port Louis has a year of London signals; a profile that re-attributed all of
/// them to Mauritius would decide they are deeply interested in a neighbourhood they have never
/// visited. Freezing the context is what lets location affinity decay honestly.
/// </para>
/// <para>
/// Every field is optional. A signal recorded before onboarding finishes has no country, and
/// refusing it would mean losing exactly the observations the cold-start path is built from.
/// </para>
/// </summary>
public sealed class SignalContext : ValueObject
{
    private SignalContext(
        string? countryCode, string? cityName, string? neighborhoodName, string? languageCode)
    {
        CountryCode = countryCode;
        CityName = cityName;
        NeighborhoodName = neighborhoodName;
        LanguageCode = languageCode;
    }

    private SignalContext() { }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string? CountryCode { get; private set; }

    public string? CityName { get; private set; }

    public string? NeighborhoodName { get; private set; }

    /// <summary>BCP 47, lower case.</summary>
    public string? LanguageCode { get; private set; }

    /// <summary>
    /// A context with nothing in it.
    /// <para>
    /// A method rather than a static field. A cached instance would be shared by every signal
    /// recorded in the process, and EF writes nulls into an owned type's columns the moment two
    /// aggregates hold one instance — a defect that surfaces as a NOT NULL violation on an unrelated
    /// row and has cost this platform real time twice.
    /// </para>
    /// </summary>
    public static SignalContext Empty() => new(null, null, null, null);

    public static SignalContext Create(
        string? countryCode,
        string? cityName = null,
        string? neighborhoodName = null,
        string? languageCode = null) =>
        new(
            Normalize(countryCode)?.ToUpperInvariant(),
            Normalize(cityName),
            Normalize(neighborhoodName),
            Normalize(languageCode)?.ToLowerInvariant());

    public bool IsEmpty =>
        CountryCode is null && CityName is null && NeighborhoodName is null && LanguageCode is null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return CityName;
        yield return NeighborhoodName;
        yield return LanguageCode;
    }
}

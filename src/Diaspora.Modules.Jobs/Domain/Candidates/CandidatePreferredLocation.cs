using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// Somewhere a candidate would work.
/// <para>
/// <b><see cref="WillRelocate"/> is what makes this list useful rather than decorative.</b> A
/// candidate in Lagos listing Berlin might already be there, or might be telling employers they
/// would move for the right role — and those are opposite signals to a recruiter deciding
/// whether the posting's relocation package is relevant. Most products collapse them and lose
/// the more valuable one.
/// </para>
/// </summary>
public sealed class CandidatePreferredLocation
{
    private CandidatePreferredLocation(
        string countryCode, string? cityName, bool willRelocate, int order)
    {
        CountryCode = countryCode;
        CityName = cityName;
        WillRelocate = willRelocate;
        Order = order;
    }

    private CandidatePreferredLocation() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>A city within it, when the candidate narrowed that far.</summary>
    public string? CityName { get; private set; }

    /// <summary>Whether the candidate would move for it, as opposed to already being there.</summary>
    public bool WillRelocate { get; private set; }

    public int Order { get; private set; }

    internal static CandidatePreferredLocation? Create(
        string? countryCode, string? cityName, bool willRelocate, int order)
    {
        var country = CandidateWorkAuthorization.NormalizeCountry(countryCode);

        if (country is null)
        {
            return null;
        }

        var city = string.IsNullOrWhiteSpace(cityName) ? null : cityName.Trim();

        if (city is { Length: > JobLimits.MaxLocationName })
        {
            city = city[..JobLimits.MaxLocationName].TrimEnd();
        }

        return new CandidatePreferredLocation(country, city, willRelocate, order);
    }
}

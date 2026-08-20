using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// Where a job is.
/// <para>
/// <b>Denormalised names beside Location's identifiers, and both are deliberate.</b> The
/// identifiers are the truth and are what a scope query narrows on; the names are what a card
/// renders, and rendering a results page of twenty postings must not mean twenty round trips
/// into B4. Location owns the gazetteer — B9 resolves against it and copies the label, and an
/// architecture test asserts no Location aggregate is reachable from this module.
/// </para>
/// <para>
/// <b>The country is required even for a fully remote posting</b>, because "remote" is never
/// actually borderless: it means remote within a jurisdiction whose employment law, tax and
/// time zone the employer has decided they can live with. A remote posting with no country is
/// the single most common way a job board wastes a candidate's afternoon.
/// </para>
/// <para>
/// <see cref="AreaPath"/> is a plain materialised string, prefix-matched with
/// <c>LIKE 'prefix%'</c> — B4's EF lesson, which B8 records too.
/// </para>
/// </summary>
public sealed class JobLocationContext : ValueObject
{
    private JobLocationContext(
        string countryCode,
        string? regionName,
        string? cityName,
        Guid? areaId,
        string? areaPath)
    {
        CountryCode = countryCode;
        RegionName = regionName;
        CityName = cityName;
        AreaId = areaId;
        AreaPath = areaPath;
    }

    private JobLocationContext() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Always present.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The region label a card renders. Null when the posting did not narrow that far.</summary>
    public string? RegionName { get; private set; }

    /// <summary>The city label a card renders.</summary>
    public string? CityName { get; private set; }

    /// <summary>Location's area identifier, when one was resolved.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>Location's materialised area path. Prefix-matched for scope narrowing.</summary>
    public string? AreaPath { get; private set; }

    public static Result<JobLocationContext> Create(
        string? countryCode,
        string? regionName = null,
        string? cityName = null,
        Guid? areaId = null,
        string? areaPath = null)
    {
        var country = NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure<JobLocationContext>(JobErrors.CountryInvalid);
        }

        var region = Clean(regionName);
        var city = Clean(cityName);
        var path = Clean(areaPath);

        if (region is { Length: > JobLimits.MaxLocationName }
            || city is { Length: > JobLimits.MaxLocationName }
            || path is { Length: > JobLimits.MaxAreaPath })
        {
            return Result.Failure<JobLocationContext>(JobErrors.LocationInvalid);
        }

        return Result.Success(new JobLocationContext(
            country, region, city, areaId == Guid.Empty ? null : areaId, path));
    }

    /// <summary>The label a card shows: the most specific place the posting named.</summary>
    public string DisplayName =>
        CityName ?? RegionName ?? CountryCode;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return RegionName;
        yield return CityName;
        yield return AreaId;
        yield return AreaPath;
    }

    private static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var candidate = countryCode.Trim().ToUpperInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterUpper) ? candidate : null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

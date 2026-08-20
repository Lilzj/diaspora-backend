using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// Where a guide's knowledge applies, denormalised from Location's catalogue.
/// <para>
/// A copy rather than a reference, because "guides for the UK" is the single most common browse
/// on the platform and it must not join across a module boundary. The country is required —
/// procedural knowledge with no jurisdiction is a blog post, not a guide — and everything below
/// it is optional precision.
/// </para>
/// <para>
/// <see cref="AreaPath"/> is a plain string, prefix-matchable with <c>LIKE 'prefix%'</c>. Plain
/// deliberately: B4 records a value-converted path member reaching a <c>StartsWith</c> and
/// failing to translate only in production, and this column exists to serve exactly that query.
/// </para>
/// </summary>
public sealed class GuideLocationContext : ValueObject
{
    private GuideLocationContext(
        string countryCode, string? regionName, string? cityName, Guid? areaId, string? areaPath)
    {
        CountryCode = countryCode;
        RegionName = regionName;
        CityName = cityName;
        AreaId = areaId;
        AreaPath = areaPath;
    }

    private GuideLocationContext() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Required — every guide has a jurisdiction.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string? RegionName { get; private set; }

    public string? CityName { get; private set; }

    /// <summary>Location's area identifier, when the guide is scoped below country level.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>The materialised hierarchy path of <see cref="AreaId"/>. Prefix-matchable.</summary>
    public string? AreaPath { get; private set; }

    public static Result<GuideLocationContext> Create(
        string? countryCode,
        string? regionName = null,
        string? cityName = null,
        Guid? areaId = null,
        string? areaPath = null)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Result.Failure<GuideLocationContext>(GuideErrors.CountryInvalid);
        }

        var country = countryCode.Trim().ToUpperInvariant();

        if (country.Length != 2 || !country.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<GuideLocationContext>(GuideErrors.CountryInvalid);
        }

        return Result.Success(new GuideLocationContext(
            country,
            Trim(regionName, GuideLimits.MaxLocationName),
            Trim(cityName, GuideLimits.MaxLocationName),
            areaId,
            Trim(areaPath, GuideLimits.MaxAreaPath)));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return RegionName;
        yield return CityName;
        yield return AreaId;
        yield return AreaPath;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}

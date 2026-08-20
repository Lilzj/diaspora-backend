using System.Globalization;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Turns a country code into a display name, once per request.
/// <para>
/// Nearly every response in this module carries a country name beside a country code — the
/// experience timeline, the location snapshot, every search result — and each one would
/// otherwise be a lookup. Caching the whole table for the life of the request costs one query
/// and 195 rows, and turns an N+1 into a constant.
/// </para>
/// <para>
/// Falls back to <see cref="RegionInfo"/> and then to the code itself, so a country the
/// catalogue has not been seeded with still renders as something rather than as blank. That
/// matters most in exactly the case it is most likely to happen: a fresh deployment whose
/// seeding has not run yet.
/// </para>
/// </summary>
public sealed class CountryNameResolver
{
    private readonly ICountryRepository _countries;

    private IReadOnlyDictionary<string, Country>? _cache;

    public CountryNameResolver(ICountryRepository countries)
    {
        _countries = countries;
    }

    public async Task<string> NameForAsync(
        CountryCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var country = await FindAsync(code, cancellationToken);

        return country?.Name.Value ?? FallbackName(code.Value);
    }

    public async Task<Country?> FindAsync(
        CountryCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var cache = await LoadAsync(cancellationToken);

        return cache.GetValueOrDefault(code.Value);
    }

    /// <summary>Every country, for a caller that is about to name several.</summary>
    public async Task<IReadOnlyDictionary<string, Country>> AllAsync(
        CancellationToken cancellationToken = default) =>
        await LoadAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<string, Country>> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var countries = await _countries.ListAllAsync(cancellationToken);

        _cache = countries.ToDictionary(
            country => country.Code.Value, StringComparer.Ordinal);

        return _cache;
    }

    /// <summary>
    /// ICU's name for the code, or the code. Derivation rather than a second name table,
    /// for the same reason Trust derives its own: a hand-maintained list of 250 names in
    /// source would be wrong within a year and wrong invisibly.
    /// </summary>
    private static string FallbackName(string code)
    {
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }
}

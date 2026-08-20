using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Search;

/// <summary>
/// A typed search for a place by name.
/// <para>
/// What the global location selector sends on every keystroke, debounced to 300 ms. The
/// minimum length is not politeness: a one-character query matches a substantial fraction of
/// a real gazetteer, and returning the first fifty of forty thousand matches is worse than
/// returning nothing, because the member reads it as "my city is not here".
/// </para>
/// </summary>
public sealed class LocationTextQuery
{
    public const int MinTermLength = 2;

    public const int MaxTermLength = 120;

    public const int MaxLimit = 50;

    public const int DefaultLimit = 20;

    private readonly AdministrativeLevel[] _levels;

    public string Term { get; }

    public IReadOnlyList<AdministrativeLevel> Levels => _levels;

    public CountryCode? CountryCode { get; }

    /// <summary>Biases ordering toward results near this point, when one is known.</summary>
    public GeoCoordinate? Near { get; }

    public int Limit { get; }

    private LocationTextQuery(
        string term,
        AdministrativeLevel[] levels,
        CountryCode? countryCode,
        GeoCoordinate? near,
        int limit)
    {
        Term = term;
        _levels = levels;
        CountryCode = countryCode;
        Near = near;
        Limit = limit;
    }

    public static Result<LocationTextQuery> Create(
        string? term,
        IEnumerable<AdministrativeLevel>? levels = null,
        CountryCode? countryCode = null,
        GeoCoordinate? near = null,
        int? limit = null)
    {
        var trimmed = term?.Trim() ?? string.Empty;

        if (trimmed.Length < MinTermLength)
        {
            return Result.Failure<LocationTextQuery>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                $"Enter at least {MinTermLength} characters to search for a place."));
        }

        if (trimmed.Length > MaxTermLength)
        {
            trimmed = trimmed[..MaxTermLength];
        }

        return Result.Success(new LocationTextQuery(
            trimmed,
            levels?.Distinct().ToArray() ?? [],
            countryCode,
            near,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)));
    }

    public bool Accepts(AdministrativeLevel level) =>
        _levels.Length == 0 || _levels.Contains(level);

    /// <summary>
    /// How well a name matches the term. Higher is better; zero is no match.
    /// <para>
    /// Ranking is deliberately crude and deliberately in the domain: an exact match beats a
    /// prefix beats a substring. That ordering is what makes typing "lon" put London above
    /// Colonia, and it is a product decision rather than a database one — a provider that
    /// pushes matching into Elasticsearch still has to reproduce this intent.
    /// </para>
    /// </summary>
    public int Score(PlaceName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.Equals(name.Value, Term, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (name.Value.StartsWith(Term, StringComparison.OrdinalIgnoreCase))
        {
            return 75;
        }

        if (name.Value.Contains(Term, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        // The endonym scores below every English match rather than above, because the query
        // arrived in whatever the member's keyboard produces and an accidental collision with
        // a foreign spelling should not outrank a deliberate English one.
        return name.LocalName?.Contains(Term, StringComparison.OrdinalIgnoreCase) == true ? 25 : 0;
    }
}

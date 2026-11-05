using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Towns;

public sealed record WorldTownId(Guid Value) : StronglyTypedId(Value)
{
    public static WorldTownId New() => new(Guid.NewGuid());

    public static WorldTownId From(Guid value) => new(value);
}

/// <summary>
/// A town from the world list, for reading a place out of a search (owner, 5 Nov 2026).
/// <para>
/// "Three bedroom flat in Grand Baie" has to find Grand Baie in Mauritius, and the platform is
/// used everywhere, so a hand-made list would never be long enough. The rows come from GeoNames
/// (<c>cities1000</c>: every populated place of a thousand people or more, about 150,000,
/// CC BY 4.0, credited in the app), loaded by <c>scripts/load-world-towns.py</c>. They are
/// reference data: nothing in the platform writes them.
/// </para>
/// <para>
/// <see cref="SearchNames"/> holds the town's names as <see cref="TownNames.Normalize"/> writes
/// them, its own, its plain-letter spelling and its other spellings, between bars
/// (<c>|grand baie|grand bay|</c>), so a candidate is found with one <c>LIKE</c>.
/// </para>
/// </summary>
public sealed class WorldTown : Entity<WorldTownId>
{
    private WorldTown() { }

    private WorldTown(
        WorldTownId id,
        int geoNameId,
        string name,
        string countryCode,
        double latitude,
        double longitude,
        long population,
        string searchNames) : base(id)
    {
        GeoNameId = geoNameId;
        Name = name;
        CountryCode = countryCode;
        Latitude = latitude;
        Longitude = longitude;
        Population = population;
        SearchNames = searchNames;
    }

    /// <summary>The town's identifier at GeoNames, so a refreshed list replaces rather than repeats.</summary>
    public int GeoNameId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public long Population { get; private set; }

    public string SearchNames { get; private set; } = string.Empty;

    /// <summary>The names <see cref="SearchNames"/> holds, one by one.</summary>
    public IEnumerable<string> Names =>
        SearchNames.Split('|', StringSplitOptions.RemoveEmptyEntries);

    public static WorldTown Create(
        int geoNameId,
        string name,
        IEnumerable<string> otherNames,
        string countryCode,
        double latitude,
        double longitude,
        long population)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(otherNames);

        // scripts/load-world-towns.py keeps the names by this same rule; change both together.
        var names = new List<string>();
        var length = 1;
        var room = TownNames.MaxNamesFor(population);

        foreach (var normalized in new[] { name }
            .Concat(otherNames.Where(spelling => !TownNames.IsCode(spelling)))
            .Select(TownNames.Normalize)
            .Where(TownNames.IsSearchable)
            .Distinct(StringComparer.Ordinal))
        {
            if (names.Count == room || length + normalized.Length + 1 > TownNames.MaxSearchNamesLength)
            {
                break;
            }

            names.Add(normalized);
            length += normalized.Length + 1;
        }

        return new WorldTown(
            WorldTownId.New(),
            geoNameId,
            name.Trim(),
            countryCode.Trim().ToUpperInvariant(),
            latitude,
            longitude,
            Math.Max(0, population),
            $"|{string.Join('|', names)}|");
    }
}

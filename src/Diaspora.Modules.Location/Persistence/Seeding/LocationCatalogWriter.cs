using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Domain.Abstractions;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// The idempotent write surface seeders are given.
/// <para>
/// Every method is get-or-create and none of them updates. That is the whole seeding
/// contract: a seeder runs on every boot, and one that overwrote would undo an operator's
/// correction on the next restart — the kind of bug that only manifests after someone has
/// already fixed the data by hand and watched it revert.
/// </para>
/// <para>
/// It also keeps an in-memory index of what it has created this run. Without it, a seeder
/// adding a city and then its neighbourhoods would issue a lookup per node against rows that
/// have not been saved yet, and either miss them or force a save between every insert.
/// </para>
/// </summary>
public sealed class LocationCatalogWriter : ILocationCatalogWriter
{
    private readonly ICountryRepository _countries;
    private readonly IAdministrativeAreaRepository _areas;
    private readonly IUnitOfWork _unitOfWork;

    private readonly Dictionary<string, Country> _pendingCountries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AdministrativeArea> _pendingAreas = new(StringComparer.Ordinal);

    public LocationCatalogWriter(
        ICountryRepository countries,
        IAdministrativeAreaRepository areas,
        IUnitOfWork unitOfWork)
    {
        _countries = countries;
        _areas = areas;
        _unitOfWork = unitOfWork;
    }

    public async Task<Country> EnsureCountryAsync(
        CountryCode code,
        PlaceName name,
        CountryMetadata? metadata = null,
        bool isSupported = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        if (_pendingCountries.TryGetValue(code.Value, out var pending))
        {
            return pending;
        }

        var existing = await _countries.GetByCodeAsync(code, cancellationToken);

        if (existing is not null)
        {
            _pendingCountries[code.Value] = existing;
            return existing;
        }

        var country = Country.Register(code, name, metadata, isSupported);
        await _countries.AddAsync(country, cancellationToken);
        _pendingCountries[code.Value] = country;

        return country;
    }

    public async Task<AdministrativeArea> EnsureAreaAsync(
        Country country,
        AdministrativeArea? parent,
        AdministrativeLevel level,
        PlaceName name,
        GeoCoordinate? center = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(country);
        ArgumentNullException.ThrowIfNull(name);

        var slug = LocationSlug.Create(name.Value);
        var key = parent is null
            ? $"{country.Code.Value}|{slug.Value}"
            : $"{parent.Id.Value}|{slug.Value}";

        if (_pendingAreas.TryGetValue(key, out var pending))
        {
            return pending;
        }

        var existing = await _areas.GetBySlugAsync(slug, level, country.Code, cancellationToken);

        if (existing is not null && existing.ParentId == parent?.Id)
        {
            _pendingAreas[key] = existing;
            return existing;
        }

        var created = parent is null
            ? AdministrativeArea.CreateUnderCountry(country, level, name, slug)
            : AdministrativeArea.CreateUnder(parent, level, name, slug);

        if (created.IsFailure)
        {
            // A seeder handing this an invalid hierarchy is a programming error, not a runtime
            // condition — the data is in the repository, not in a request.
            throw new InvalidOperationException(
                $"Cannot seed '{name.Value}' as a {level}: {created.Error.Description}");
        }

        var area = created.Value;

        if (center is not null)
        {
            area.SetCenter(center);
        }

        await _areas.AddAsync(area, cancellationToken);
        _pendingAreas[key] = area;

        return area;
    }

    public Task<bool> HasAreasAsync(CancellationToken cancellationToken = default) =>
        _areas.AnyAsync(cancellationToken);

    /// <summary>
    /// Commits everything accumulated this run.
    /// <para>
    /// Called by the seed runner rather than by a seeder, so a chain of seeders lands in one
    /// save. A country seeder that saved 195 rows and then a city seeder that saved 20 would
    /// leave a half-populated catalogue behind if the second failed.
    /// </para>
    /// </summary>
    internal Task<int> CommitAsync(CancellationToken cancellationToken) =>
        _unitOfWork.SaveChangesAsync(cancellationToken);
}

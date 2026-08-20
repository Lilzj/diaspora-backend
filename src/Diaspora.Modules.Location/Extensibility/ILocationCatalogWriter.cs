using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// The narrow, idempotent write surface a <see cref="ILocationCatalogSeeder"/> is given.
/// <para>
/// Deliberately not the repository. A seeder that held <see cref="ICountryRepository"/> could
/// delete, could rewrite a country's support status, could move a subtree — none of which a
/// seeder has any business doing, and all of which would be discovered the hard way. This
/// interface can only add what is missing, which is the whole of the seeding contract.
/// </para>
/// </summary>
public interface ILocationCatalogWriter
{
    /// <summary>
    /// Ensures a country exists, and returns it either way. Existing rows are left alone.
    /// </summary>
    Task<Country> EnsureCountryAsync(
        CountryCode code,
        PlaceName name,
        CountryMetadata? metadata = null,
        bool isSupported = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a node exists beneath a country, and returns it either way.
    /// </summary>
    Task<AdministrativeArea> EnsureAreaAsync(
        Country country,
        AdministrativeArea? parent,
        AdministrativeLevel level,
        PlaceName name,
        GeoCoordinate? center = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the catalogue already has any geography below country level.</summary>
    Task<bool> HasAreasAsync(CancellationToken cancellationToken = default);
}

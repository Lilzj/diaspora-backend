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

    /// <summary>
    /// Gives an existing node its outline, when exactly one node matches and it has none.
    /// <para>
    /// Matched on country, level and exact name, never on anything looser: an outline filed
    /// against the wrong "Port Louis" would redraw a district as a city. A node that already has
    /// an outline keeps it, unless the stored one is the same shape from the same source taken
    /// earlier — so a refreshed extract can replace itself, and nothing can replace an outline
    /// somebody else supplied.
    /// </para>
    /// </summary>
    Task<BoundaryImportOutcome> EnsureBoundaryAsync(
        CountryCode countryCode,
        AdministrativeLevel level,
        PlaceName name,
        GeographicBoundary boundary,
        BoundaryProvenance provenance,
        CancellationToken cancellationToken = default);
}

/// <summary>What <see cref="ILocationCatalogWriter.EnsureBoundaryAsync"/> did, for the seeder's log.</summary>
public enum BoundaryImportOutcome
{
    /// <summary>The node had no outline and now has this one.</summary>
    Recorded,

    /// <summary>The node held an earlier extract of the same shape, now replaced.</summary>
    Refreshed,

    /// <summary>The node already had this outline, or one from elsewhere that is not ours to replace.</summary>
    AlreadyOutlined,

    /// <summary>No node has that country, level and name.</summary>
    NoSuchArea,

    /// <summary>More than one node does, and guessing between them is how a line lands on the wrong town.</summary>
    Ambiguous,

    /// <summary>
    /// The node's own centre is outside the outline — the match is on a name, and this is the check
    /// that the name meant the same place.
    /// </summary>
    CentreOutside
}

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented to add geography to the catalogue at startup.
/// <para>
/// Geography is data, and the module ships only enough of it to be useful on day one: 195
/// countries, and the cities and neighbourhoods the frontend's own fixtures name. A real
/// gazetteer — GeoNames, OpenStreetMap extracts, a licensed dataset — is an import job, and
/// this is where it plugs in. Recorded as known gap #3 in the frontend contract report.
/// </para>
/// <para>
/// Seeders must be idempotent. They run on every boot, and a seeder that inserted
/// unconditionally would duplicate the catalogue on the second start.
/// </para>
/// </summary>
public interface ILocationCatalogSeeder
{
    /// <summary>Lower runs first. Countries must exist before anything hangs off them.</summary>
    int Order => 0;

    /// <summary>A name for logs and for the diagnostics endpoint.</summary>
    string Name { get; }

    /// <summary>Adds whatever is missing. Must not modify what is already there.</summary>
    Task SeedAsync(ILocationCatalogWriter writer, CancellationToken cancellationToken = default);
}

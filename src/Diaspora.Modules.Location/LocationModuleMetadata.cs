namespace Diaspora.Modules.Location;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class LocationModuleMetadata
{
    public const string Name = "Location";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's own database schema. It owns these tables and no others — the shared
    /// <c>DiasporaDbContext</c> verifies that at startup.
    /// </summary>
    public const string Schema = "location";

    /// <summary>
    /// The route prefix every Location endpoint hangs off.
    /// <para>
    /// The frontend calls a top-level <c>/neighborhood/{id}</c> today. That path belongs to
    /// no module, so it is not claimed here: the neighbourhood hub is served at
    /// <c>/api/v1/location/neighborhoods/{idOrSlug}</c>, which resolves the client's existing
    /// slug unchanged. Recorded as intentional deviation #2 in the frontend contract report.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/location";
}

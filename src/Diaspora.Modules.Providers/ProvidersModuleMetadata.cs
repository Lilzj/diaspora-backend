namespace Diaspora.Modules.Providers;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class ProvidersModuleMetadata
{
    public const string Name = "Providers";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's own database schema. It owns these tables and no others — the shared
    /// <c>DiasporaDbContext</c> verifies that at startup.
    /// </summary>
    public const string Schema = "providers";

    /// <summary>
    /// The route prefix every Providers endpoint hangs off.
    /// <para>
    /// One prefix, not several, and deliberately <c>providers</c> rather than the client's
    /// <c>/listings</c>. "Listing" names four different aggregates in the frontend — provider,
    /// housing, marketplace and job — and the first module to claim <c>/listings</c> would take
    /// it from the other three. <c>providers</c> is also the resource-type key B4's contract
    /// freeze already reserved for this module, so the name is consistent across the estate
    /// before a single row exists.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/providers";
}

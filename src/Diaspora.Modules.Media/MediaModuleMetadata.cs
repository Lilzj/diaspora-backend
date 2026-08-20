namespace Diaspora.Modules.Media;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class MediaModuleMetadata
{
    public const string Name = "Media";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's own database schema. It owns these tables and no others — the shared
    /// <c>DiasporaDbContext</c> verifies that at startup.
    /// </summary>
    public const string Schema = "media";

    /// <summary>
    /// The route prefix every Media endpoint hangs off.
    /// <para>
    /// Deliberately not a top-level <c>/upload</c> or <c>/files</c>. Those read well in a demo
    /// and collide with the first module that wants them; every module on this platform owns
    /// exactly one prefix, and this is Media's.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/media";
}

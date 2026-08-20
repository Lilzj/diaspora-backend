namespace Diaspora.Modules.Community;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class CommunityModuleMetadata
{
    public const string Name = "Community";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's own database schema. It owns these tables and no others — the shared
    /// <c>DiasporaDbContext</c> verifies that at startup.
    /// </summary>
    public const string Schema = "community";

    /// <summary>
    /// The route prefix every Community endpoint hangs off.
    /// <para>
    /// One prefix, not several. The temptation here is real — <c>/posts</c>, <c>/comments</c>,
    /// <c>/feeds</c> and <c>/reactions</c> all read beautifully at the top level, and each one
    /// would be claimed forever by the first module to think of it. Every module on this
    /// platform owns exactly one prefix, and this is Community's.
    /// </para>
    /// <para>
    /// Note what is <em>not</em> claimed: <c>/api/v1/bookmarks</c> belongs to the B2.6
    /// reference module and stays there. Community's bookmarks live under
    /// <c>/api/v1/community/bookmarks</c> and are a different capability — content bookmarks
    /// with collections and folders, rather than the reference module's flat list.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/community";
}

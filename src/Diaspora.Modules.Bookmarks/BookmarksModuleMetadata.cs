namespace Diaspora.Modules.Bookmarks;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class BookmarksModuleMetadata
{
    public const string Name = "Bookmarks";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "bookmarks";

    /// <summary>
    /// The one place the module's public URL space is declared. Versioning is baked into the
    /// prefix: a breaking change to the wire contract is a new prefix, not a mutation of this one.
    /// <para>
    /// <b>Added in B1.4.1, and the string is the one already being served.</b> The endpoint
    /// contributor spelled <c>/api/v1/bookmarks</c> as a literal, which routed correctly and was
    /// invisible to <c>Every_declared_route_prefix_is_versioned_and_owned_by_one_module</c> — the
    /// rule reads the constants, so a module that declares none is the one module the rule cannot
    /// speak for. B1.4 recorded that as F6. Declaring the constant changes no URL.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/bookmarks";
}

/// <summary>The permissions the Bookmarks module owns.</summary>
public static class BookmarkPermissions
{
    public const string Read = "bookmarks:read";
    public const string Manage = "bookmarks:manage";
}

/// <summary>The feature gates the Bookmarks module owns.</summary>
public static class BookmarkFeatures
{
    public const string Use = "bookmarks.use";
}

/// <summary>The authorisation policies the Bookmarks module's endpoints reference.</summary>
public static class BookmarkPolicies
{
    public const string Read = "bookmarks.read";
    public const string Manage = "bookmarks.manage";
}

/// <summary>The Bookmarks module's telemetry source name.</summary>
public static class BookmarkTelemetry
{
    public const string SourceName = "Diaspora.Bookmarks";
}

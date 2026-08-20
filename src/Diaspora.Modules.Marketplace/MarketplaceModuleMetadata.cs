namespace Diaspora.Modules.Marketplace;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class MarketplaceModuleMetadata
{
    public const string Name = "Marketplace";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "marketplace";

    /// <summary>
    /// The one place the module's public URL space is declared. Versioning is baked into the
    /// prefix: a breaking change to the wire contract is a new prefix, not a mutation of this
    /// one. An architecture test asserts this is the module's only route prefix.
    /// <para>
    /// <c>/marketplace</c> rather than <c>/listings</c>, and B7 records why: "listing" is a word
    /// housing, marketplace and jobs all want, and the first module to claim <c>/listings</c>
    /// would take it from the other two.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/marketplace";
}

namespace Diaspora.Modules.Housing;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class HousingModuleMetadata
{
    public const string Name = "Housing";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "housing";

    /// <summary>
    /// The one place the module's public URL space is declared. Versioning is baked into the
    /// prefix: a breaking change to the wire contract is a new prefix, not a mutation of this
    /// one. An architecture test asserts this is the module's only route prefix.
    /// <para>
    /// <c>/housing</c> rather than <c>/listings</c>, and B7 records why: "listing" is a word
    /// housing, marketplace and jobs all want, and the first module to claim <c>/listings</c>
    /// would take it from the other two. B10 declined it for the same reason and so does this.
    /// </para>
    /// <para>
    /// The neighbourhood surface lives under this prefix too, at <c>/housing/neighborhoods</c>,
    /// and that placement is a deliberate admission rather than a claim: Housing owns the
    /// neighbourhood <em>profile</em>, not the neighbourhood. The place itself is B4's and is
    /// addressed at <c>/api/v1/location</c>, which is where a client resolves a slug to an area
    /// before asking this module what living there is like.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/housing";
}

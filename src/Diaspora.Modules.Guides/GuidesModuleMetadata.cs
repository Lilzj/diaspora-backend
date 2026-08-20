namespace Diaspora.Modules.Guides;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class GuidesModuleMetadata
{
    public const string Name = "Guides";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "guides";

    /// <summary>
    /// The module's guide URL space. Versioning is baked into the prefix: a breaking change to the
    /// wire contract is a new prefix, not a mutation of this one.
    /// </summary>
    public const string RoutePrefix = "/api/v1/guides";

    /// <summary>
    /// The Country Knowledge Directory's URL space (B8.1).
    /// <para>
    /// <b>A second top-level prefix, and the decision is deliberate rather than convenient.</b> The
    /// obvious alternative was nesting it under <c>/api/v1/guides/knowledge</c>, which would have
    /// kept the module to one prefix and would have told every client something untrue: that the
    /// canonical way to discover country-specific knowledge is a sub-resource of guides. It is not —
    /// it is the entry point, and guides are what it discovers. Jobs, Housing, Marketplace and
    /// Community Life all consume this surface, and a URL that reads as an implementation detail of
    /// B8 would invite each of them to go around it to the thing it appears to belong to.
    /// </para>
    /// <para>
    /// The invariant the architecture test protects is unchanged and is the one that matters:
    /// <b>every route prefix in this assembly is declared here and is versioned.</b> Two declared
    /// prefixes are fine; one undeclared one is not.
    /// </para>
    /// </summary>
    public const string KnowledgeRoutePrefix = "/api/v1/knowledge";

    /// <summary>
    /// The Country Intelligence Platform's URL space (B8.2).
    /// <para>
    /// <b>A third top-level prefix on one module, and the reasoning is B8.1's carried forward.</b>
    /// Nesting official notices under <c>/api/v1/knowledge/intelligence</c> would tell every client
    /// something untrue: that what a government announced is a sub-resource of what a community has
    /// written about it. It is not — it is the authoritative layer, and the Knowledge Directory is
    /// what discovers the community half. A URL reading as an implementation detail of curation would
    /// invite a client to treat an immigration circular as a kind of guide.
    /// </para>
    /// <para>
    /// The invariant the architecture test protects is unchanged and is the one that matters:
    /// <b>every route prefix in this assembly is declared here and is versioned.</b> Three declared
    /// prefixes are fine; one undeclared one is not.
    /// </para>
    /// </summary>
    public const string IntelligenceRoutePrefix = "/api/v1/intelligence";
}

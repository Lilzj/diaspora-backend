namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What this deployment's Knowledge Directory can actually do.
/// <para>
/// <b>Every seam in this phase is optional, replaceable or additive, which means no client can
/// assume what is installed.</b> A deployment may register three extra section providers, replace
/// the search engine, turn caching off and receive context bands from four verticals — and a client
/// that hard-coded the shipped list would render a page missing half of it. This endpoint is how a
/// client discovers the shape of the deployment it is talking to, and it is the same answer B8
/// gives for its own seams.
/// </para>
/// </summary>
public sealed record KnowledgeCapabilitiesResponse
{
    /// <summary>Section keys registered here, in render order.</summary>
    public IReadOnlyList<string> Sections { get; init; } = [];

    /// <summary>Sort keys the discovery endpoints accept.</summary>
    public IReadOnlyList<string> Sorts { get; init; } = [];

    /// <summary>Difficulty values the discovery filters accept.</summary>
    public IReadOnlyList<string> Difficulties { get; init; } = [];

    /// <summary>The registered search engine's name.</summary>
    public required string SearchEngine { get; init; }

    /// <summary>Whether search can answer right now.</summary>
    public required bool SearchAvailable { get; init; }

    /// <summary>The registered cache implementation, and whether it is on.</summary>
    public required string CacheProvider { get; init; }

    public required bool CachingEnabled { get; init; }

    /// <summary>Modules contributing bands to the country dashboard.</summary>
    public IReadOnlyList<string> ContextModules { get; init; } = [];

    /// <summary>Analytics sinks receiving usage signals.</summary>
    public IReadOnlyList<string> AnalyticsSinks { get; init; } = [];

    /// <summary>Navigation rows in the catalogue, active ones only.</summary>
    public required int CategoryCount { get; init; }

    /// <summary>Countries with a published directory.</summary>
    public required int PublishedCountryCount { get; init; }

    // ── The ceilings a curation client should enforce before it submits ─────────────────────

    public required int MaxDirectoryEntries { get; init; }

    public required int MaxFeaturedGuides { get; init; }

    public required int MaxPathSteps { get; init; }

    public required int MaxRecommendedReads { get; init; }

    public required int MaxPageSize { get; init; }
}

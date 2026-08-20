namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// The counts a results page renders beside its filters — "Germany (412)", "Remote (88)".
/// <para>
/// <b>Every group is optional and an empty set is a valid answer.</b> Facets are expensive on
/// some engines and free on others, and an engine that cannot compute them cheaply should say so
/// rather than compute them expensively — the client renders the filters without counts, which
/// is a slightly poorer page rather than a slow one.
/// </para>
/// <para>
/// Keyed by the same strings the request filters on, so a client can round-trip a facet into a
/// filter without a translation table.
/// </para>
/// </summary>
public sealed record JobSearchFacets
{
    /// <summary>Upper-case ISO 3166-1 alpha-2 to count.</summary>
    public IReadOnlyDictionary<string, int> Countries { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Category key to count.</summary>
    public IReadOnlyDictionary<string, int> Categories { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary><c>OnSite</c>, <c>Hybrid</c>, <c>Remote</c> to count.</summary>
    public IReadOnlyDictionary<string, int> WorkplaceTypes { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary><c>FullTime</c>, <c>Contract</c> and so on, to count.</summary>
    public IReadOnlyDictionary<string, int> JobTypes { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Experience level to count.</summary>
    public IReadOnlyDictionary<string, int> ExperienceLevels { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>How many of the matching postings say they sponsor a visa.</summary>
    public int VisaSponsorshipCount { get; init; }

    /// <summary>How many come from an employer carrying a Trust mark.</summary>
    public int VerifiedEmployerCount { get; init; }

    /// <summary>The engine computed no facets. Rendered as filters without counts.</summary>
    public static JobSearchFacets None { get; } = new();
}

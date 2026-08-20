namespace Diaspora.Platform.Operations.Search;

/// <summary>
/// A document as a search index sees it.
/// <para>
/// Deliberately weakly typed. A search index is not a database: it holds a flattened projection of
/// something whose real shape lives in a module this assembly must never reference. The alternative
/// — a generic <c>ISearchProvider&lt;TDocument&gt;</c> — would make the provider adapter generic over
/// every module's document type, which is the same thing as a project reference to every module.
/// </para>
/// </summary>
/// <param name="Id">Stable identity within the index. Re-indexing the same id replaces the document.</param>
/// <param name="Index">Which index this belongs to.</param>
/// <param name="Fields">
/// Flat field values. Strings, numbers, booleans, dates and arrays of those — nothing that needs a
/// mapping the provider cannot infer.
/// </param>
/// <param name="Version">
/// Monotonic per document. An out-of-order index write with a lower version is discarded, which is
/// what stops a slow retry of an old edit overwriting a newer one.
/// </param>
public sealed record SearchDocument(
    string Id,
    string Index,
    IReadOnlyDictionary<string, object?> Fields,
    long Version);

/// <summary>How a field participates in ranking. The only ranking knob the abstraction exposes.</summary>
public enum SearchFieldKind
{
    /// <summary>Free text, analysed. The default for anything a member typed.</summary>
    Text = 0,

    /// <summary>Exact match only — a key, a code, an identifier.</summary>
    Keyword = 1,

    /// <summary>Numeric. Filterable and sortable.</summary>
    Number = 2,

    /// <summary>Date or timestamp.</summary>
    Date = 3,

    /// <summary>Boolean.</summary>
    Boolean = 4,

    /// <summary>Latitude/longitude pair, for distance filters and sorts.</summary>
    GeoPoint = 5
}

/// <summary>One field in an index.</summary>
/// <param name="Name">Field name, as it appears in <see cref="SearchDocument.Fields"/>.</param>
/// <param name="Kind">How the field is treated.</param>
/// <param name="Boost">
/// Relative weight when matching. <c>1.0</c> is neutral. A title normally outranks a body; a
/// description rarely outranks either.
/// </param>
/// <param name="Facetable">Whether the field can be aggregated into facet counts.</param>
public sealed record SearchFieldDefinition(
    string Name,
    SearchFieldKind Kind,
    double Boost = 1.0,
    bool Facetable = false);

/// <summary>
/// An index a module declares.
/// <para>
/// Declared through <see cref="ISearchIndexContributor"/> so the provider adapter can create and
/// migrate indexes without any module referencing it, and so the operational dashboard can list
/// every index the platform believes exists beside every index the provider actually has.
/// </para>
/// </summary>
/// <param name="Name">Index name. Lower case, namespaced to the owning module.</param>
/// <param name="Module">Owning module.</param>
/// <param name="Fields">The fields.</param>
/// <param name="DefaultSearchFields">
/// Fields a query with no field named searches. Empty means every <see cref="SearchFieldKind.Text"/>
/// field.
/// </param>
public sealed record SearchIndexDefinition(
    string Name,
    string Module,
    IReadOnlyList<SearchFieldDefinition> Fields,
    IReadOnlyList<string> DefaultSearchFields);

/// <summary>A search request, provider-independent.</summary>
/// <param name="Index">Which index to search.</param>
/// <param name="Text">The member's query. May be empty for a pure filter.</param>
/// <param name="Filters">Exact-match filters, ANDed. A field mapped to several values is ORed within itself.</param>
/// <param name="Skip">Offset. Present because the module ports it replaces are offset-paged.</param>
/// <param name="Take">Page size.</param>
/// <param name="SortField">Field to sort by. Null sorts by relevance.</param>
/// <param name="SortDescending">Sort direction.</param>
/// <param name="Facets">Fields to compute facet counts for.</param>
public sealed record SearchQuery(
    string Index,
    string? Text = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Filters = null,
    int Skip = 0,
    int Take = 20,
    string? SortField = null,
    bool SortDescending = false,
    IReadOnlyList<string>? Facets = null);

/// <summary>One match.</summary>
/// <param name="Id">The document's id, which the caller resolves against its own store.</param>
/// <param name="Score">Relevance. Comparable within one result set and meaningless across two.</param>
/// <param name="Fields">Fields the provider returned, when it returns any.</param>
public sealed record SearchHit(string Id, double Score, IReadOnlyDictionary<string, object?>? Fields = null);

/// <summary>A result set.</summary>
/// <param name="Hits">The matches, best first.</param>
/// <param name="TotalCount">
/// Total matches, which a provider may estimate. The dashboard reports whether the number is exact.
/// </param>
/// <param name="TotalIsExact">Whether <paramref name="TotalCount"/> is counted rather than estimated.</param>
/// <param name="Facets">Facet counts, when asked for.</param>
/// <param name="ElapsedMilliseconds">What the provider says it took.</param>
public sealed record SearchResult(
    IReadOnlyList<SearchHit> Hits,
    long TotalCount,
    bool TotalIsExact,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>? Facets,
    double ElapsedMilliseconds)
{
    public static SearchResult Empty { get; } = new([], 0, true, null, 0);
}

/// <summary>What the platform knows about one index right now.</summary>
/// <param name="Index">The index name.</param>
/// <param name="DocumentCount">Documents the provider reports holding.</param>
/// <param name="LastIndexedAt">When the most recent document was written, if known.</param>
/// <param name="PendingDocuments">Documents queued and not yet written.</param>
/// <param name="IsAvailable">Whether the provider can serve this index.</param>
public sealed record SearchIndexState(
    string Index,
    long DocumentCount,
    DateTimeOffset? LastIndexedAt,
    int PendingDocuments,
    bool IsAvailable);

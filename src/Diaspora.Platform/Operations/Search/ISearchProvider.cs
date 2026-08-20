namespace Diaspora.Platform.Operations.Search;

/// <summary>
/// The single seam a search engine registers into.
/// <para>
/// <b>What this is, and what it is not.</b> Nine modules already own a search port — B6, B8, B8.1,
/// B8.2, B9, B10, B11, B12 and B14 each ship one with a database-backed default, recorded as
/// DEBT-020. Those ports are correct and they stay: each expresses its module's query in its
/// module's vocabulary, which is what made the defaults good enough to ship. What was missing is a
/// place for the engine itself to be registered once instead of nine times, and a place for indexing
/// and synchronisation to live — which is what this is.
/// </para>
/// <para>
/// <b>Nothing implements this in B17, deliberately.</b> The phase brief says to prepare for
/// OpenSearch and not to implement it, and there is a stronger reason than scope: an implementation
/// written against no real index and no real query load would encode guesses that the first real
/// deployment would have to unpick. The seam is the deliverable; the adapter is a registration.
/// </para>
/// <para>
/// The migration path is per module and per index: a module's search port gains an adapter that
/// delegates to this provider, the module's own engine stays registered as the fallback, and the
/// module chooses between them with a feature flag. No module needs to change to make the provider
/// available, and no module is forced to use it when it arrives.
/// </para>
/// </summary>
public interface ISearchProvider
{
    /// <summary>A name for diagnostics and metric labels — <c>opensearch</c>, <c>none</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Whether this provider can answer right now. False makes every caller fall back to its own
    /// engine, which is why a missing search cluster degrades search rather than breaking it.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a query.</summary>
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>What the provider reports about an index.</summary>
    Task<SearchIndexState> DescribeAsync(string index, CancellationToken cancellationToken = default);
}

/// <summary>
/// The write side.
/// <para>
/// Separate from <see cref="ISearchProvider"/> because the two have different availability
/// requirements and different failure handling. A read that cannot reach the index falls back to the
/// database and the member sees results; a write that cannot reach the index must be retried or the
/// index silently drifts from the source of truth — which is the failure mode that makes people stop
/// trusting search.
/// </para>
/// </summary>
public interface ISearchIndexer
{
    /// <summary>Creates or migrates an index to match its declared definition. Idempotent.</summary>
    Task EnsureIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Writes documents. A document whose version is not greater than the stored one is skipped.</summary>
    Task IndexAsync(IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>Removes documents by id.</summary>
    Task DeleteAsync(string index, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps an index in step with the module that owns the data.
/// <para>
/// Implemented by the owning module, not by the provider. Only the module knows which of its rows
/// belong in the index, what a document looks like, and how to enumerate the ones that changed —
/// and asking the provider to know would be the project reference this whole design exists to avoid.
/// </para>
/// <para>
/// <b>Two methods because there are two jobs.</b> Incremental synchronisation is the steady state
/// and runs constantly; a full rebuild is what happens after a mapping change, a restore, or the
/// discovery that the index has drifted. A design with only the first has no answer to "the index is
/// wrong"; a design with only the second reindexes the platform every few minutes.
/// </para>
/// </summary>
public interface ISearchSynchronizer
{
    /// <summary>The index this keeps current.</summary>
    string Index { get; }

    /// <summary>The owning module.</summary>
    string Module { get; }

    /// <summary>
    /// Documents changed since <paramref name="since"/>, in batches.
    /// <para>
    /// <paramref name="since"/> is the watermark the background indexer persists. A synchronizer must
    /// treat it as inclusive-ish and tolerate re-sending a document it has already sent: an index
    /// write is idempotent by version, and a watermark that tried to be exact would drop the rows
    /// that changed during the same tick.
    /// </para>
    /// </summary>
    IAsyncEnumerable<SearchDocument> EnumerateChangedAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Every document, for a rebuild.</summary>
    IAsyncEnumerable<SearchDocument> EnumerateAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Declares a module's indexes to the platform.</summary>
public interface ISearchIndexContributor
{
    string Module { get; }

    IEnumerable<SearchIndexDefinition> Indexes { get; }
}

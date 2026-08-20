namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// The OpenSearch-independence port: how job search is asked and how the index is fed.
/// <para>
/// <b>B9 exposes search projections; it does not implement search.</b> The shipped
/// <c>EfJobSearchEngine</c> answers with a LIKE-style filter over the write model, which is
/// honest and adequate until a real engine arrives. A search module replaces the whole
/// implementation by registering its own class — the port is engine-independent, the documents
/// it ingests are flat primitives, and nothing above this interface changes. The brief's
/// requirement that "future OpenSearch integration must require infrastructure changes only" is
/// this interface.
/// </para>
/// <para>
/// <b><see cref="SearchAsync"/> answers with identifiers, never with documents.</b> The read
/// path hydrates cards from the write model, so a stale index can misrank a result but can never
/// misrender one — and, more importantly, can never leak a posting whose visibility changed
/// after it was indexed. The repository re-applies the audience rules to whatever comes back.
/// </para>
/// <para>
/// <b>Index maintenance is best-effort by contract.</b> <see cref="IndexAsync"/> and
/// <see cref="RemoveAsync"/> are called after the posting write has committed, and a failure
/// degrades to a stale index entry, never to a rolled-back publication. The one asymmetry worth
/// stating: a failed <see cref="RemoveAsync"/> leaves a closed or hidden posting discoverable,
/// which is worse than a failed index, so callers log removal failures at a higher level and the
/// reconciliation sweep re-checks them.
/// </para>
/// </summary>
public interface IJobSearchEngine
{
    /// <summary>Upserts one posting's document. Idempotent on the posting identifier.</summary>
    Task IndexAsync(JobSearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts many at once. The reindex sweep's entry point — an engine that batches natively
    /// should override the obvious loop.
    /// </summary>
    Task IndexManyAsync(
        IReadOnlyCollection<JobSearchDocument> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a posting from the index — closed, filled, expired, archived, hidden or deleted.
    /// </summary>
    Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers a search with identifiers and a continuation, never with documents.
    /// </summary>
    Task<JobSearchOutcome> SearchAsync(
        JobSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Facet counts for the filters a results page renders beside itself — countries, categories,
    /// workplace types. Returning an empty set is a valid answer: an engine that cannot compute
    /// facets cheaply should say so rather than compute them expensively, and the client renders
    /// the filters without counts.
    /// </summary>
    Task<JobSearchFacets> GetFacetsAsync(
        JobSearchRequest request, CancellationToken cancellationToken = default);
}

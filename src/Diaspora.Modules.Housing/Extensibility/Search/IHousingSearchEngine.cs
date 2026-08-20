namespace Diaspora.Modules.Housing.Extensibility.Search;

/// <summary>
/// The port an OpenSearch adapter satisfies later without a domain change.
/// <para>
/// <b>The brief asks that a future search implementation require infrastructure changes only, and
/// this interface is the whole of that promise.</b> Housing ships an EF-backed engine that answers
/// from the write model — honest, adequate, and entirely without a relevance score or geometry. A
/// real engine implements this contract, is registered after the shipped one, and replaces it
/// wholesale. Nothing above this port changes: not the handlers, not the responses, not the
/// endpoints, not the tests that exercise them.
/// </para>
/// <para>
/// <b>The request is deliberately expressed in the module's own vocabulary rather than in a query
/// language.</b> An engine receives "two bedrooms, under 900 a month in GBP, pets allowed, in this
/// neighbourhood" and decides for itself whether that becomes a bool query, a filter chain or a
/// SQL predicate. A port that took a query string would have frozen one engine's dialect into the
/// domain.
/// </para>
/// <para>
/// <b>Facets are part of the contract, not an afterthought.</b> A housing search without counts
/// beside the filters makes a member guess which narrowing will empty the page — and on a search
/// where the good results vanish within hours, an empty page is a member who gives up.
/// </para>
/// </summary>
public interface IHousingSearchEngine
{
    /// <summary>
    /// What this engine calls itself. Surfaced in the search response so an operator can tell
    /// which engine answered without reading a log.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this engine can serve the request at all.
    /// <para>
    /// A cheap synchronous check. An external engine that has not finished indexing, or that does
    /// not support a filter combination, says so here and the caller falls back to the shipped EF
    /// engine — which is the difference between a degraded search and a broken one.
    /// </para>
    /// </summary>
    bool CanServe(HousingSearchRequest request) => true;

    /// <summary>Runs the search.</summary>
    Task<HousingSearchOutcome> SearchAsync(
        HousingSearchRequest request, CancellationToken cancellationToken = default);
}

using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>
/// How events are found.
/// <para>
/// <b>The port the brief's "future OpenSearch integration must require infrastructure changes only"
/// requirement turns on.</b> The module ships an EF implementation that answers from the write
/// model; an OpenSearch adapter registers after it and replaces it wholesale. Nothing above this
/// interface — no handler, no endpoint, no contract — changes when it does.
/// </para>
/// <para>
/// <b>The filter is <see cref="EventQueryFilter"/>, the module's own type, and that is
/// deliberate.</b> A search-specific query type would have to be kept in step with the domain
/// filter by hand, and the first facet somebody added to one and not the other would produce a
/// search that quietly ignored it. One type means one place to add a facet.
/// </para>
/// <para>
/// <b>Registered last-wins rather than as a collection.</b> There is one live engine, not a chain:
/// two engines answering one query would need a merge nobody can define, because scores from
/// different engines are not commensurable.
/// </para>
/// </summary>
public interface ICommunityEventSearchEngine
{
    /// <summary>What this engine is, for diagnostics and for the readiness endpoint.</summary>
    string EngineName { get; }

    Task<EventSearchResult> SearchAsync(
        EventQueryFilter filter, CancellationToken cancellationToken = default);
}

namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// The port every knowledge search goes through.
/// <para>
/// <b>Provider-independent, and the brief's success criterion is that a future OpenSearch
/// integration is an infrastructure change and nothing more.</b> This interface is what makes that
/// literally true: the request carries primitives and enumerable filters, the outcome carries
/// identifiers and a cursor, and no query handler, response contract, endpoint or client above this
/// line knows what is behind it. B8.1 ships an EF-backed engine that answers from the write model;
/// a search module that ships its own registers after it and replaces it wholesale.
/// </para>
/// <para>
/// <b>Registered last-wins rather than as a collection</b>, exactly as B8 registers its own guide
/// search engine. There is one live engine, not a chain of them — two engines answering one query
/// would have to be merged, and merging two relevance orderings produces a third that is nobody's.
/// </para>
/// <para>
/// <b>Deliberately not a ranking seam.</b> Ordering within a shelf is B8's <c>IGuideRankingStrategy</c>
/// and B8.1 reuses it rather than declaring a second one — a platform with two answers to
/// "what does most helpful mean" has no answer.
/// </para>
/// </summary>
public interface IKnowledgeSearchEngine
{
    /// <summary>The engine's name, for diagnostics and the capabilities endpoint.</summary>
    string Name { get; }

    /// <summary>
    /// Whether the engine can answer right now.
    /// <para>
    /// An external index that is unreachable says no, and the caller returns a service-unavailable
    /// error rather than an empty result — because an empty result reads as "there is nothing about
    /// visas for this country", which is a much worse thing to tell somebody than "search is down".
    /// </para>
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<KnowledgeSearchOutcome> SearchAsync(
        KnowledgeSearchRequest request, CancellationToken cancellationToken = default);
}

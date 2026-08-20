namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// The port every official notice search goes through.
/// <para>
/// <b>Provider-independent, and the brief's success criterion is that a future OpenSearch integration
/// is an infrastructure change and nothing more.</b> This interface is what makes that literally
/// true: the request carries primitives, the outcome carries identifiers and a cursor, and no query
/// handler, response contract, endpoint or client above this line knows what is behind it. B8.2 ships
/// an EF-backed engine that answers from the write model; a search module that ships its own
/// registers after it and replaces it wholesale.
/// </para>
/// <para>
/// <b>Registered last-wins rather than as a collection</b>, exactly as B8 and B8.1 register theirs.
/// There is one live engine, not a chain of them — two engines answering one query would have to be
/// merged, and merging two relevance orderings produces a third that is nobody's.
/// </para>
/// <para>
/// <b>An unavailable engine must be reported as unavailable, never as an empty answer.</b> This
/// matters more here than anywhere else on the platform: "no results" on an official notice search
/// reads as "the government has said nothing about this", and telling somebody that when the truth is
/// that an index is down is the single worst failure mode this module has.
/// </para>
/// </summary>
public interface IOfficialNoticeSearchEngine
{
    /// <summary>The engine's name, for diagnostics and the capabilities endpoint.</summary>
    string Name { get; }

    /// <inheritdoc cref="IKnowledgeSearchEngine.IsAvailableAsync" />
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<NoticeSearchOutcome> SearchAsync(
        NoticeSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offers a notice to the index.
    /// <para>
    /// An honest no-op for the shipped engine, whose index <em>is</em> the write model — the same
    /// degenerate-case trick B8 uses, and what keeps the abstraction from being a lie. An external
    /// engine does real work here.
    /// </para>
    /// </summary>
    Task IndexAsync(Guid officialNoticeId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IndexAsync" />
    Task RemoveAsync(Guid officialNoticeId, CancellationToken cancellationToken = default);
}

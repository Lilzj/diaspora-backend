using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Search;

/// <summary>
/// Searches a country's organised knowledge.
/// <para>
/// <b>Distinct from B8's guide search, and the difference is what is being searched for.</b> A guide
/// search finds documents. A knowledge search finds documents <em>within a country's organised
/// knowledge</em>: it filters by navigation row rather than filing category, understands "official"
/// as a narrower thing than "verified", and takes a reading-time ceiling seriously because somebody
/// standing in a queue at an immigration office has five minutes.
/// </para>
/// <para>
/// Every search is counted, per country and per phrase, with no reader identity anywhere. A phrase
/// that finds nothing is the platform being told exactly what to commission next, by the person who
/// wanted it.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record SearchKnowledgeQuery : IQuery<KnowledgeSearchResponse>
{
    public required SearchKnowledgeRequest Request { get; init; }
}

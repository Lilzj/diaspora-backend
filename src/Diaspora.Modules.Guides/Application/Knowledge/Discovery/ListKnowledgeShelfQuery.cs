using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// One paged shelf of a country's knowledge.
/// <para>
/// <b>The single query behind the six named discovery endpoints the brief lists.</b> Featured,
/// popular, recently updated, beginner, official and verified differ in one predicate each; the
/// clamping, filtering, cursor handling and mapping are identical, and six handlers would be five
/// copies waiting to drift. The endpoints stay six, which is what a client actually consumes.
/// </para>
/// <para>
/// Unauthenticated, like every read here. Visibility is still applied inside — an anonymous caller
/// and a signed-in one both see published guides and nothing else.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListKnowledgeShelfQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    /// <summary>Which shelf. Chosen by the endpoint, never by the caller.</summary>
    public required KnowledgeShelf Shelf { get; init; }

    public required BrowseKnowledgeRequest Request { get; init; }
}

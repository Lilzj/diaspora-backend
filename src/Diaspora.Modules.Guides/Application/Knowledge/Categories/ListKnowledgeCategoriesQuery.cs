using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// The whole navigation catalogue, ordered.
/// <para>
/// Unauthenticated, like every read in this phase. Small by design and never paged: the catalogue is
/// what a country page renders as a menu, and a menu that needs pagination has stopped being one.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListKnowledgeCategoriesQuery : IQuery<IReadOnlyList<KnowledgeCategoryResponse>>
{
    /// <summary>
    /// Archived rows too.
    /// <para>
    /// Honoured only for a curator. A reader asking for archived rows gets the active ones, rather
    /// than an error — the parameter is a curator's tool and a reader who sends it is a client bug,
    /// not an attack, and failing the catalogue read would break their whole page.
    /// </para>
    /// </summary>
    public bool IncludeArchived { get; init; }

    /// <summary>A BCP-47 tag. Labels come back resolved for it.</summary>
    public string? Locale { get; init; }
}

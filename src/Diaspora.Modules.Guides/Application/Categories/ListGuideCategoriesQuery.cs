using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// The guide catalogue.
/// <para>
/// The first call a client makes: the Guides tab renders its category chip row before anything
/// else, and the editor's category picker is built from the same payload. Returned flat and
/// ordered, so a client can draw a row or assemble the shallow tree from one response.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListGuideCategoriesQuery : IQuery<IReadOnlyList<GuideCategoryResponse>>
{
    /// <summary>Only this node's children, by parent key. Null returns the whole catalogue.</summary>
    public string? ParentKey { get; init; }

    /// <summary>Staff-only. Deactivated categories still resolve; they are not offered.</summary>
    public bool IncludeInactive { get; init; }
}

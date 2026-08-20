using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Adds a navigation row to the Knowledge Directory's catalogue.
/// <para>
/// <b>The key is the contract and can never be changed.</b> It is what a contributor seed declares,
/// what a URL carries and what every country directory entry points at — renaming it would strand
/// every entry that referenced it and break deep links that were correct when they were made.
/// Everything else about a row is editable.
/// </para>
/// <para>
/// Gated at <see cref="KnowledgePermissions.ManageCategories"/>, which is role-scoped to
/// administrators: the navigation catalogue is the shape of the whole knowledge product, not a
/// per-country editorial choice.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageCategories)]
[Auditable]
public sealed record CreateKnowledgeCategoryCommand : ICommand<KnowledgeCategoryResponse>
{
    public required CreateKnowledgeCategoryRequest Request { get; init; }
}

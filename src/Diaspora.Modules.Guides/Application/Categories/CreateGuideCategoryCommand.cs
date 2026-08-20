using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// Adds a category to the guide catalogue.
/// <para>
/// <b>The key is the contract and can never be changed.</b> It is what a contributor seed
/// declares, what a URL carries and what every guide filed under it stores — renaming it would
/// strand guides and break deep links that were correct when they were made. Everything else
/// about a category is editable.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.ManageCategories"/>, which is role-scoped to
/// administrators: the taxonomy is the shape of the whole knowledge base, not a per-member
/// preference.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ManageCategories)]
[Auditable]
public sealed record CreateGuideCategoryCommand : ICommand<GuideCategoryResponse>
{
    public required CreateGuideCategoryRequest Request { get; init; }
}

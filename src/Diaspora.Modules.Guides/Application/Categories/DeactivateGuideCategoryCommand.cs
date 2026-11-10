using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// Retires a category from the picker.
/// <para>
/// <b>Retire, never delete.</b> Guides store category keys, so deleting an entry would strand
/// every guide that ever chose it — and the ones that did were filed correctly at the time.
/// Deactivating stops new guides choosing it and leaves the existing ones readable.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.ManageCategories)]
[Auditable]
public sealed record DeactivateGuideCategoryCommand : ICommand<GuideCategoryResponse>
{
    public required string Key { get; init; }
}

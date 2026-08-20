using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// Renames or re-describes a catalogue entry, addressed by its immutable key.
/// <para>
/// The key is the route value precisely because it is the thing that cannot change: an update
/// that could rewrite its own address would be an update no client could follow.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ManageCategories)]
[Auditable]
public sealed record UpdateGuideCategoryCommand : ICommand<GuideCategoryResponse>
{
    /// <summary>The category's stable key. Never itself editable.</summary>
    public required string Key { get; init; }

    public required UpdateGuideCategoryRequest Request { get; init; }
}

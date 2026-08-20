using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Rewrites a navigation row, addressed by its stable key.
/// <para>
/// Every collection on the request is a replacement rather than a patch, which is why the revision
/// matters: a client working from stale state finds out rather than silently deleting somebody's
/// translations.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageCategories)]
[Auditable]
public sealed record UpdateKnowledgeCategoryCommand : ICommand<KnowledgeCategoryResponse>
{
    /// <summary>The stable key. From the route, because it is the resource's identity.</summary>
    public required string Key { get; init; }

    public required UpdateKnowledgeCategoryRequest Request { get; init; }
}

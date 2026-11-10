using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Renders the current draft exactly as the detail screen would render it published.
/// <para>
/// The last thing an author does before publishing, and worth its own query rather than a flag
/// on the detail read: preview is explicitly "show me the unpublished thing", and a boolean on a
/// public endpoint that switches a draft on is a permission check waiting to be forgotten.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.ViewDrafts)]
public sealed record PreviewGuideQuery : IQuery<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

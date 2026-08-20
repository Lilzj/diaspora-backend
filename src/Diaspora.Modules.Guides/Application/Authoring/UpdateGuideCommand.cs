using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// The full-document editor save: metadata, and the whole structure when the client sends one.
/// <para>
/// Wholesale rather than per-block, because that is how the editor works. The request carries the
/// revision the document was loaded at; a stale save comes back as a conflict the client can
/// merge from rather than silently overwriting a colleague's edit.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.EditOwn"/>; whether this particular guide is the caller's
/// is the handler's second question, answered through <c>GuideAuthorizationService</c> so staff
/// can edit any guide without co-authoring it.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.EditOwn)]
[Auditable]
public sealed record UpdateGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required UpdateGuideRequest Request { get; init; }
}

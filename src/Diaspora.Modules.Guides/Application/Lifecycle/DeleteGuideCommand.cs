using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Removes a guide from the platform.
/// <para>
/// <b>Soft, and the softness is the platform's job rather than this module's.</b> The handler
/// hands the aggregate to the repository's removal path and the shared persistence pipeline
/// stamps the deletion; a handler that set the flags itself is the B1.0.1 workaround an
/// architecture test now forbids.
/// </para>
/// <para>
/// Nothing is returned. A response describing a guide that has just stopped existing invites a
/// client to render it.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.DeleteOwn)]
[Auditable]
public sealed record DeleteGuideCommand : ICommand
{
    public required Guid GuideId { get; init; }
}

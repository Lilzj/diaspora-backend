using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// A moderator takes a member's file down. Staff only, always with a reason.
/// <para>
/// <b>The one door staff have onto a member's file.</b> Until 7 Nov 2026 anyone holding
/// <c>media:administer</c> passed the management check, so a moderator could delete, edit,
/// re-attach or reorder any member's files through the member's own routes, with no reason and no
/// notice. Those routes now refuse staff, and an illegal or abusive file comes down here instead.
/// </para>
/// </summary>
[Authorize(Permission = MediaPermissions.Administer)]
[Auditable(Action = "media.moderate")]
public sealed record ModerateMediaCommand : ICommand<MediaModerationResponse>
{
    public required Guid MediaId { get; init; }

    public required ModerateMediaRequest Request { get; init; }
}

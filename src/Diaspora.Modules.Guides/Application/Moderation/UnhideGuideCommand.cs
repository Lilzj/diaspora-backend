using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>
/// Lifts a moderation hide, restoring exactly what was there.
/// <para>
/// No reason is asked for. Requiring one to <em>undo</em> a restriction would put friction on
/// the side of the decision that should be easy — the moderation trail already records who hid
/// it and why, and a note can be added deliberately when the reversal is worth explaining.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Moderate)]
[Auditable]
public sealed record UnhideGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

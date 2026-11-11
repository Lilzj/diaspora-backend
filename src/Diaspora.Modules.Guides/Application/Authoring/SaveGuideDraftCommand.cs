using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// The editor's auto-save: the current structure kept aside, without touching the guide.
/// <para>
/// <b>It is not a save of the guide.</b> Nothing on the aggregate changes — the snapshot is a
/// separate, append-only aggregate whose only job is to survive a closed laptop. That is what
/// makes it cheap enough to fire every few seconds of typing, and it is why an auto-save can
/// never conflict with, overwrite or lose a real save.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.EditOwn"/>, like every other write to somebody's draft.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record SaveGuideDraftCommand : ICommand
{
    public required Guid GuideId { get; init; }

    public required SaveGuideDraftRequest Request { get; init; }
}

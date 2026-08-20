using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Applies an old version's content back onto the working draft.
/// <para>
/// <b>Not time travel for readers.</b> The version itself is immutable and untouched; what
/// changes is the draft, exactly as if the author had retyped the old text. Publishing
/// afterwards is what mints the new version — so a restore leaves a forward-only history rather
/// than a history that rewrites itself.
/// </para>
/// <para>
/// <c>[Transactional]</c> because the restore writes the guide and the analytics row that counts
/// version adoptions — the signal that recent edits made things worse, and worthless if it can
/// go missing when the guide write succeeds.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.EditOwn)]
[Transactional(Reason = "The restored draft and the version-adoption analytics must commit together.")]
[Auditable]
public sealed record RestoreGuideVersionCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    /// <summary>The version number to restore, as the version list renders it.</summary>
    public required int VersionNumber { get; init; }

    public required RestoreGuideVersionRequest Request { get; init; }
}

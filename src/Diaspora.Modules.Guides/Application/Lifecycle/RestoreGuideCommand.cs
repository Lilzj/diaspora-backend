using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Returns an archived guide to publication, exactly as it was.
/// <para>
/// No new version is minted: the content never changed, only its audience did. Minting one
/// would put an empty entry in the history for an act that changed nothing a reader can see.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.RestoreOwn)]
[Auditable]
public sealed record RestoreGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

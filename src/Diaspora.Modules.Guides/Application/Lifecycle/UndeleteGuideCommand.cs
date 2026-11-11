using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Puts a deleted guide back. The guide resumes in whatever status it held.
/// <para>
/// The counterpart to a soft delete, and the reason the platform soft-deletes at all: an author
/// who deleted the wrong draft gets it back, and a moderator reviewing a takedown has the
/// content to review. Nothing about the guide's publication state is touched — a deleted draft
/// comes back a draft.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.RestoreOwn)]
[Auditable]
public sealed record UndeleteGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

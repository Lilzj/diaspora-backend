using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>
/// Removes a guide from every public surface, reversibly.
/// <para>
/// The guide's own state is untouched — it stays published, keeps its versions, and its author
/// can still see and fix it. Lifting the decision restores exactly what was there, which is what
/// makes hiding the right first response to a report: it is reversible in a way a delete is not.
/// </para>
/// <para>
/// <b>The reason is required.</b> A moderation action nobody can explain six months later is an
/// action that should not have been possible, and the aggregate refuses an empty one.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Moderate)]
[Auditable]
public sealed record HideGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required HideGuideRequest Request { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Withdraws editorial placement. Idempotent — un-featuring what is not featured succeeds,
/// because the caller's intent is already the world's state.
/// </summary>
[Authorize(Permission = GuidePermissions.Feature)]
[Auditable]
public sealed record UnfeatureGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

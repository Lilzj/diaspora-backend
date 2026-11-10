using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Calls a scheduled publication off before it fires.
/// <para>
/// No request body: there is exactly one thing to cancel, and the guide already knows what it
/// is. Cancelling a guide with nothing scheduled is a conflict rather than a silent success —
/// the author believed something was pending, and saying "done" would leave them believing it.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Schedule)]
[Auditable]
public sealed record CancelScheduledPublicationCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}

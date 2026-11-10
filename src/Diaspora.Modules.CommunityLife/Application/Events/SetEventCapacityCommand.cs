using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Changes how many places an event has.
/// <para>
/// Refused below what is already committed: the sixty-first person was told they had a place, and
/// shrinking the hall under them would take it back silently.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record SetEventCapacityCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required SetEventCapacityRequest Request { get; init; }
}

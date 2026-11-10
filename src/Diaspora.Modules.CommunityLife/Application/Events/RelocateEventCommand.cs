using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Moves an event in space, or between in-person and online.</summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record RelocateEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required RelocateEventRequest Request { get; init; }
}

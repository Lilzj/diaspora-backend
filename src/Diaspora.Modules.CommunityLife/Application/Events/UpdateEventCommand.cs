using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Edits an event's descriptive content.</summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record UpdateEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required UpdateEventRequest Request { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Replaces an event's running order.</summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record SetEventAgendaCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required SetEventAgendaRequest Request { get; init; }
}

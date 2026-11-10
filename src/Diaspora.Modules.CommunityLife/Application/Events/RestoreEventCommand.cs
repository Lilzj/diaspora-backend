using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Brings an archived or deleted event back, as a draft.</summary>
[RequirePermission(CommunityLifePermissions.EventRestore)]
[Auditable]
public sealed record RestoreEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }
}

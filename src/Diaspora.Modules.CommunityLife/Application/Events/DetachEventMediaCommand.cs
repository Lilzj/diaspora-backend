using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Removes an asset from an event.</summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record DetachEventMediaCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required Guid MediaId { get; init; }
}

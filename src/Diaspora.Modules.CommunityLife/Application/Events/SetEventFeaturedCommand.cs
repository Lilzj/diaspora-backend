using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Puts an event on the front of the browse screen, or takes it off. Staff only.</summary>
[Authorize(Permission = CommunityLifePermissions.EventFeature)]
[Auditable]
public sealed record SetEventFeaturedCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required SetFeaturedRequest Request { get; init; }
}

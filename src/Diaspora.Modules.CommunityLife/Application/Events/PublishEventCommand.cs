using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Makes an event discoverable.</summary>
[Authorize(Permission = CommunityLifePermissions.EventPublish)]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Events)]
public sealed record PublishEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required PublishEventRequest Request { get; init; }
}

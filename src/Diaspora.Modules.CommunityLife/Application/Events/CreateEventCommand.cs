using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Drafts an event.
/// <para>
/// Transactional: the event and its analytics record commit together, so views on an unlisted link
/// are counted from the moment the link exists.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventCreate)]
[Transactional(Reason = "Writes the event and its analytics record.")]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Events)]
public sealed record CreateEventCommand : ICommand<EventResponse>
{
    public required CreateEventRequest Request { get; init; }
}

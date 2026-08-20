using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Files an event away.</summary>
[Authorize(Permission = CommunityLifePermissions.EventArchive)]
[Auditable]
public sealed record ArchiveEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required ArchiveEventRequest Request { get; init; }
}

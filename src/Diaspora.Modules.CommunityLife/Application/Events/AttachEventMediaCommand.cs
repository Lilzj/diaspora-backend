using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Attaches an uploaded asset to an event.</summary>
[Authorize(Permission = CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record AttachEventMediaCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required MediaAttachmentRequest Request { get; init; }
}

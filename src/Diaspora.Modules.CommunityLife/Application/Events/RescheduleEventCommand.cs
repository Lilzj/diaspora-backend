using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Moves an event in time.
/// <para>
/// A material change: everybody who said they were coming has planned around the old time, which is
/// why the aggregate marks it as one and consumers notify on it.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record RescheduleEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required RescheduleEventRequest Request { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Calls an event off and releases every place.
/// <para>
/// Transactional: the event and all of its responses move together. A cancellation that committed
/// without releasing the guest list would leave attendees holding places at something that is not
/// happening, and counting against their attendance record when it does not.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventCancel)]
[Transactional(Reason = "Cancels the event and releases every response against it.")]
[Auditable]
public sealed record CancelEventCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required CancelEventRequest Request { get; init; }
}

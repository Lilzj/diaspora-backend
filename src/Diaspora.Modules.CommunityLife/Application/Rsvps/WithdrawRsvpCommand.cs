using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Withdraws the caller from an event.
/// <para>
/// Transactional: releasing the place and promoting whoever was next off the waiting list happen
/// together, because a place that is free between two requests is a place two people can be given.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.Rsvp)]
[Transactional(Reason = "Releases the place and promotes the next person waiting.")]
[Auditable]
public sealed record WithdrawRsvpCommand : ICommand<RsvpResponse>
{
    public required Guid EventId { get; init; }

    public required WithdrawRsvpRequest Request { get; init; }
}

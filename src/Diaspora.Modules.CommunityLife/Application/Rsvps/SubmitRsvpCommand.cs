using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Says whether the caller is coming, and with how many people.
/// <para>
/// Transactional: the response and the event's counters commit together. A place taken without the
/// count moving is a place two people can be told they have.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.Rsvp)]
[Transactional(Reason = "Takes or releases a place against the event capacity.")]
[Auditable]
public sealed record SubmitRsvpCommand : ICommand<RsvpResponse>
{
    public required Guid EventId { get; init; }

    public required SubmitRsvpRequest Request { get; init; }
}

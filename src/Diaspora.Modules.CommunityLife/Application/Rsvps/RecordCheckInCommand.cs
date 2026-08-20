using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>Records somebody arriving at an event.</summary>
[Authorize(Permission = CommunityLifePermissions.CheckInManage)]
[Auditable]
public sealed record RecordCheckInCommand : ICommand<RsvpResponse>
{
    public required Guid EventId { get; init; }

    public required CheckInRequest Request { get; init; }
}

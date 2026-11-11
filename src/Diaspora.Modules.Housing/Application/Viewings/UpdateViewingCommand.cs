using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// Moves a viewing along: schedule, complete, cancel or record a no-show.
/// <para>
/// One command for every transition rather than four, because the client renders one action bar and
/// which moves are legal depends on where the viewing already is - which the aggregate knows and
/// the client should not have to.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ViewingManage)]
[Auditable]
public sealed record UpdateViewingCommand : ICommand<HousingViewingResponse>
{
    public required Guid ViewingId { get; init; }

    public required UpdateViewingRequest Request { get; init; }
}

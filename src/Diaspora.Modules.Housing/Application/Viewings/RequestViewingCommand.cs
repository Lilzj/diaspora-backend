using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// Asks to see a property.
/// <para>
/// Verified only, and that is the sharpest access line in the module: a confirmed viewing is what
/// unlocks a property address, and a member who could arrange viewings without proving who they are
/// could map the empty homes in a city.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ViewingSchedule)]
[Auditable]
public sealed record RequestViewingCommand : ICommand<HousingViewingResponse>
{
    public required Guid ListingId { get; init; }

    public required RequestViewingRequest Request { get; init; }
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>
/// Moves the caller's active discovery location.
/// <para>
/// Writes through to the travel destination while travel mode is on, so picking a place from
/// the selector abroad does not silently overwrite where the member lives.
/// </para>
/// </summary>
[Authorize(Permission = LocationPermissions.ManageProfile)]
public sealed record SetCurrentLocationCommand : ICommand<LocationProfileResponse>
{
    public required SetCurrentLocationRequest Location { get; init; }
}

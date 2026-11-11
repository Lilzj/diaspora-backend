using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Turns travel mode on or off, optionally setting the destination in the same call.</summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record SetTravelModeCommand : ICommand<LocationProfileResponse>
{
    public required bool Enabled { get; init; }

    public SetCurrentLocationRequest? Destination { get; init; }
}

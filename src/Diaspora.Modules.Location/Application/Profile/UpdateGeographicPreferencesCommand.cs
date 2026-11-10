using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Changes how geography behaves for the caller. Null fields are left alone.</summary>
[RequirePermission(LocationPermissions.ManageProfile)]
public sealed record UpdateGeographicPreferencesCommand : ICommand<LocationProfileResponse>
{
    public required UpdateGeographicPreferencesRequest Preferences { get; init; }
}

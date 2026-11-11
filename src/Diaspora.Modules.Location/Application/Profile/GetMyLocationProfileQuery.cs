using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>
/// The caller's whole geographic context.
/// <para>
/// One fetch rehydrates the client's location store — current location, radius, saved places,
/// recents, travel mode and permission status — on any device.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadProfile)]
public sealed record GetMyLocationProfileQuery : IQuery<LocationProfileResponse>;

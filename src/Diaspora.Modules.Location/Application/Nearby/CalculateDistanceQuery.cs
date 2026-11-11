using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// How far apart two points are, with commute estimates.
/// <para>
/// The client already computes this locally and will keep doing so — a round trip to render a
/// badge would be absurd. The endpoint exists so that a server-side consumer (a notification,
/// a digest, a recommendation) gets the same number the member sees, from the same earth
/// model and the same constants.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.Hyperlocal)]
public sealed record CalculateDistanceQuery : IQuery<DistanceResponse>
{
    public required double FromLatitude { get; init; }

    public required double FromLongitude { get; init; }

    public required double ToLatitude { get; init; }

    public required double ToLongitude { get; init; }
}

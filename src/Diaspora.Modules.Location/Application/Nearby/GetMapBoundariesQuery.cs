using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>The place outlines a map viewport overlaps.</summary>
[Authorize(Permission = LocationPermissions.Hyperlocal)]
public sealed record GetMapBoundariesQuery : IQuery<MapBoundaryPageResponse>
{
    /// <summary>The viewport: <c>north,south,east,west</c>.</summary>
    public required string Bounds { get; init; }

    public string? CountryCode { get; init; }
}

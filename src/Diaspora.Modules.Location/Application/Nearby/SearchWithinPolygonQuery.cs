using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>The catalogue nodes inside a drawn outline.</summary>
[RequirePermission(LocationPermissions.Hyperlocal)]
public sealed record SearchWithinPolygonQuery : IQuery<IReadOnlyList<AreaSummaryResponse>>
{
    public required IReadOnlyList<GeoCoordinate> Vertices { get; init; }

    public IReadOnlyList<AdministrativeLevel> Levels { get; init; } = [];

    public string? CountryCode { get; init; }

    public int? Limit { get; init; }
}

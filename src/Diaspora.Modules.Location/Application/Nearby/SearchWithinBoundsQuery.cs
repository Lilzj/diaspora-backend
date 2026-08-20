using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// The catalogue nodes inside a viewport — the query behind a map that can be panned.
/// </summary>
[Authorize(Permission = LocationPermissions.Hyperlocal)]
public sealed record SearchWithinBoundsQuery : IQuery<IReadOnlyList<AreaSummaryResponse>>
{
    public required double North { get; init; }

    public required double South { get; init; }

    public required double East { get; init; }

    public required double West { get; init; }

    public IReadOnlyList<AdministrativeLevel> Levels { get; init; } = [];

    public string? CountryCode { get; init; }

    public int? Limit { get; init; }
}

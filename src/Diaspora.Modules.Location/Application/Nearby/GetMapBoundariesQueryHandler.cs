using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Answers "which places' outlines should this map draw".
/// <para>
/// <b>Overlap, not centres.</b> A map scrolled to the edge of Quatre Bornes shows the Quatre Bornes
/// line with the town's centre off screen, so the question is whether an outline's extent overlaps
/// the viewport — never whether the place's centre is inside it, which is what every other
/// viewport read on this module asks.
/// </para>
/// <para>
/// <b>Wide views get nothing, and say so.</b> An outline is a few hundred points; a viewport wider
/// than a country would ask for every one the catalogue holds and draw lines too fine to see.
/// Beyond <see cref="MaxSpanDegrees"/> the page is empty and marked incomplete, which a client can
/// tell apart from "no outlines here".
/// </para>
/// </summary>
public sealed class GetMapBoundariesQueryHandler
    : IQueryHandler<GetMapBoundariesQuery, MapBoundaryPageResponse>
{
    /// <summary>The most outlines one page carries.</summary>
    public const int MaxBoundaries = 20;

    /// <summary>The widest viewport, in degrees of latitude or longitude, that outlines are drawn for.</summary>
    public const double MaxSpanDegrees = 12d;

    private readonly IAdministrativeAreaRepository _areas;

    public GetMapBoundariesQueryHandler(IAdministrativeAreaRepository areas) => _areas = areas;

    public async Task<Result<MapBoundaryPageResponse>> Handle(
        GetMapBoundariesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bounds = BoundingBox.TryParse(request.Bounds);

        if (bounds.IsFailure)
        {
            return Result.Failure<MapBoundaryPageResponse>(bounds.Error);
        }

        CountryCode? country = null;

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var parsed = CountryCode.TryCreate(request.CountryCode);

            if (parsed.IsFailure)
            {
                return Result.Failure<MapBoundaryPageResponse>(parsed.Error);
            }

            country = parsed.Value;
        }

        var box = bounds.Value;
        var width = box.CrossesAntimeridian ? 360d - (box.West - box.East) : box.East - box.West;

        if (box.North - box.South > MaxSpanDegrees || width > MaxSpanDegrees)
        {
            return Result.Success(Page([], box, isComplete: false));
        }

        // One more than a page, to know whether the page is all of them.
        var overlapping = await _areas.GetOutlinedOverlappingAsync(
            box, [], country, MaxBoundaries + 1, cancellationToken);

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "boundaries"));

        var drawn = overlapping
            .Take(MaxBoundaries)
            .OrderBy(area => area.Level)
            .ThenBy(area => area.Name.Value, StringComparer.Ordinal)
            .ToList();

        return Result.Success(Page(drawn, box, isComplete: overlapping.Count <= MaxBoundaries));
    }

    private static MapBoundaryPageResponse Page(
        IReadOnlyList<AdministrativeArea> areas, BoundingBox bounds, bool isComplete) =>
        new()
        {
            Boundaries =
            [
                .. areas.Select(area => new MapBoundaryResponse
                {
                    AreaId = area.Id.Value,
                    Name = area.Name.Value,
                    Slug = area.Slug.Value,
                    Level = area.Level.ToString(),
                    CountryCode = area.CountryCode.Value,
                    Boundary = LocationMapper.ToBoundary(area.Boundary, area.BoundaryProvenance)!
                })
            ],
            Attributions =
            [
                .. areas
                    .Select(area => area.BoundaryProvenance?.Attribution)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
            ],
            Bounds = LocationMapper.ToBounds(bounds)!,
            IsComplete = isComplete
        };
}

using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Answers "which places' outlines should this map draw" — since 14 Oct 2026, none.
/// <para>
/// <b>Outlines are retired with the place catalogue</b> (owner decision, Phase 2B). The route is
/// kept so an installed app that still asks gets a well-formed empty page rather than a 404, and
/// Phase 3 removes it. Nothing here reads the catalogue's stored OpenStreetMap boundaries any more.
/// The viewport is still validated, so a malformed request is still refused as one.
/// </para>
/// </summary>
public sealed class GetMapBoundariesQueryHandler
    : IQueryHandler<GetMapBoundariesQuery, MapBoundaryPageResponse>
{
    /// <summary>The reason every page carries.</summary>
    public const string RetiredReason = "outlines-retired";

    public Task<Result<MapBoundaryPageResponse>> Handle(
        GetMapBoundariesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bounds = BoundingBox.TryParse(request.Bounds);

        if (bounds.IsFailure)
        {
            return Task.FromResult(Result.Failure<MapBoundaryPageResponse>(bounds.Error));
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var parsed = CountryCode.TryCreate(request.CountryCode);

            if (parsed.IsFailure)
            {
                return Task.FromResult(Result.Failure<MapBoundaryPageResponse>(parsed.Error));
            }
        }

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "boundaries"));

        return Task.FromResult(Result.Success(new MapBoundaryPageResponse
        {
            Boundaries = [],
            Attributions = [],
            Bounds = LocationMapper.ToBounds(bounds.Value)!,

            // Complete: there are no outlines to draw anywhere, so nothing is missing from this one.
            IsComplete = true,
            Reason = RetiredReason
        }));
    }
}

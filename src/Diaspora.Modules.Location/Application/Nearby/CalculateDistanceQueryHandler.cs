using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>Serves point-to-point distance and commute estimates.</summary>
public sealed class CalculateDistanceQueryHandler
    : IQueryHandler<CalculateDistanceQuery, DistanceResponse>
{
    private readonly IGeoDistanceCalculator _calculator;

    public CalculateDistanceQueryHandler(IGeoDistanceCalculator calculator)
    {
        _calculator = calculator;
    }

    public Task<Result<DistanceResponse>> Handle(
        CalculateDistanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = GeoCoordinate.TryCreate(request.FromLatitude, request.FromLongitude);

        if (origin.IsFailure)
        {
            return Task.FromResult(Result.Failure<DistanceResponse>(origin.Error));
        }

        var destination = GeoCoordinate.TryCreate(request.ToLatitude, request.ToLongitude);

        if (destination.IsFailure)
        {
            return Task.FromResult(Result.Failure<DistanceResponse>(destination.Error));
        }

        var distance = _calculator.Between(origin.Value, destination.Value);

        return Task.FromResult(Result.Success(new DistanceResponse
        {
            DistanceKm = distance.Kilometers,
            DistanceMiles = distance.Miles,
            Formatted = DistancePresentation.Format(distance),
            CommuteMinutes = DistancePresentation.AllCommuteModes(distance)
        }));
    }
}

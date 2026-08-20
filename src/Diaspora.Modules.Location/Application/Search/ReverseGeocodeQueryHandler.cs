using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Search;

/// <summary>Serves coordinate-to-place resolution through the configured geocoder.</summary>
public sealed class ReverseGeocodeQueryHandler
    : IQueryHandler<ReverseGeocodeQuery, LocationReferenceResponse>
{
    /// <summary>
    /// Where each confidence band ends, in kilometres.
    /// <para>
    /// Bands rather than a raw number alone, because a client rendering "we think you are in
    /// Reading" needs a decision and not an arithmetic problem. The distance travels too, so a
    /// caller that wants its own threshold still has everything it needs.
    /// </para>
    /// </summary>
    private const double ExactWithinKm = 1d;

    private const double HighWithinKm = 5d;

    private const double MediumWithinKm = 25d;

    private readonly IGeocodingProvider _geocoder;
    private readonly IGeoDistanceCalculator _calculator;

    public ReverseGeocodeQueryHandler(
        IGeocodingProvider geocoder, IGeoDistanceCalculator calculator)
    {
        _geocoder = geocoder;
        _calculator = calculator;
    }

    public async Task<Result<LocationReferenceResponse>> Handle(
        ReverseGeocodeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coordinate = GeoCoordinate.TryCreate(request.Latitude, request.Longitude);

        if (coordinate.IsFailure)
        {
            return Result.Failure<LocationReferenceResponse>(coordinate.Error);
        }

        var resolved = await _geocoder.ReverseGeocodeAsync(coordinate.Value, cancellationToken);

        LocationTelemetry.ReverseGeocodes.Add(
            1, new KeyValuePair<string, object?>("resolved", resolved.IsSuccess));

        if (resolved.IsFailure)
        {
            return Result.Failure<LocationReferenceResponse>(resolved.Error);
        }

        // Exact precision: the caller supplied these coordinates, so returning them at reduced
        // precision would be theatre. The privacy setting governs disclosure of a member's
        // stored position, which is a different question.
        var reference = LocationMapper.ToReference(resolved.Value, CoordinatePrecision.Exact);

        if (resolved.Value.Coordinate is not { } resolvedCoordinate)
        {
            // A place with no centre — a country resolved from nothing else. No distance can be
            // measured, so none is claimed and the confidence stays null rather than optimistic.
            return Result.Success(reference);
        }

        var distance = _calculator.Between(coordinate.Value, resolvedCoordinate).Kilometers;

        return Result.Success(reference with
        {
            DistanceKm = distance,
            Confidence = BandFor(distance)
        });
    }

    private static string BandFor(double distanceKm) => distanceKm switch
    {
        < ExactWithinKm => "Exact",
        < HighWithinKm => "High",
        < MediumWithinKm => "Medium",
        _ => "Low"
    };
}

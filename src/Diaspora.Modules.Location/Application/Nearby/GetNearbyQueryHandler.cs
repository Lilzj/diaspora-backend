using System.Diagnostics;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Location.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Resolves where the member means, asks every vertical what it has there, and returns one
/// payload.
/// <para>
/// Note how little of this handler is geography: the scope resolver does that, and the
/// aggregator does the fan-out. What is left is the part neither of them owns — turning a
/// resolved scope into the response envelope, and deciding what the response looks like when
/// nothing could be resolved at all.
/// </para>
/// </summary>
public sealed class GetNearbyQueryHandler : IQueryHandler<GetNearbyQuery, NearbyResponse>
{
    private readonly IGeographicScopeResolver _scopes;
    private readonly NearbyAggregator _aggregator;
    private readonly ICountryRepository _countries;
    private readonly ICurrentUser _currentUser;

    public GetNearbyQueryHandler(
        IGeographicScopeResolver scopes,
        NearbyAggregator aggregator,
        ICountryRepository countries,
        ICurrentUser currentUser)
    {
        _scopes = scopes;
        _aggregator = aggregator;
        _countries = countries;
        _currentUser = currentUser;
    }

    public async Task<Result<NearbyResponse>> Handle(
        GetNearbyQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = LocationTelemetry.Activity.StartActivity("location.nearby");
        var stopwatch = Stopwatch.StartNew();

        // Resolved without the catalogue (14 Oct 2026): a point is a circle, a town is its name,
        // and an area id from an old app build narrows nothing.
        var scope = await _scopes.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                UserId = _currentUser.UserId
            },
            cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<NearbyResponse>(scope.Error);
        }

        // A proximity question with no point of reference is not a proximity question. Failing
        // is better than returning everything: a client that sent no location has a bug, and
        // an unbounded fan-out across every vertical is the most expensive way to hide it.
        if (scope.Value.Search is null || scope.Value.Center is null)
        {
            return Result.Failure<NearbyResponse>(LocationErrors.AreaNotFound);
        }

        var aggregate = await _aggregator.AggregateAsync(
            new NearbyResourceRequest
            {
                Search = scope.Value.Search,
                UserId = _currentUser.UserId,
                CountryCode = scope.Value.CountryCode,
                CityName = scope.Value.CityName,

                // Clamped to the domain's own cap so a client cannot ask one vertical for ten
                // thousand rows by putting a large number in a query string.
                Limit = Math.Clamp(
                    request.Limit ?? NearbySearch.DefaultLimit, 1, NearbySearch.MaxLimit)
            },
            cancellationToken);

        var center = await BuildCenterAsync(scope.Value, cancellationToken);

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "nearby"));

        LocationTelemetry.SpatialQueryDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("kind", "nearby"));

        var groups = Filter(aggregate.Groups, request.ResourceTypes);

        return Result.Success(new NearbyResponse
        {
            Center = center,
            RadiusKm = scope.Value.Radius.Scope == RadiusScope.Radius
                ? scope.Value.Radius.Distance.Kilometers
                : 0d,
            Scope = scope.Value.Radius.Scope.ToString(),
            Groups = groups,
            UnavailableResourceTypes = aggregate.UnavailableResourceTypes
        });
    }

    private static Dictionary<string, IReadOnlyList<NearbyItemResponse>> Filter(
        IReadOnlyDictionary<string, IReadOnlyList<NearbyResourceItem>> groups,
        IReadOnlyList<string> resourceTypes)
    {
        var wanted = resourceTypes.Count == 0
            ? null
            : new HashSet<string>(resourceTypes, StringComparer.OrdinalIgnoreCase);

        return groups
            .Where(group => wanted is null || wanted.Contains(group.Key))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NearbyItemResponse>)group.Value
                    .Select(ToResponse)
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static NearbyItemResponse ToResponse(NearbyResourceItem item) =>
        new()
        {
            Id = item.Id,
            Title = item.Title,
            Category = item.Category,
            DistanceKm = item.DistanceKm,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            CityName = item.CityName,
            AreaName = item.AreaName,
            ImageUrl = item.ImageUrl,
            PriceText = item.PriceText,
            Rating = item.Rating,
            Payload = item.Payload
        };

    /// <summary>
    /// Describes the search centre as a location reference: the point, the town the request or the
    /// member's saved location named, and the country. Null when no country is known — a point is
    /// no longer resolved to a catalogue node, so there is nothing to infer one from.
    /// </summary>
    private async Task<LocationReferenceResponse?> BuildCenterAsync(
        GeographicScope scope, CancellationToken cancellationToken)
    {
        if (scope.CountryCode is null)
        {
            return null;
        }

        var country = await _countries.GetByCodeAsync(scope.CountryCode, cancellationToken);

        if (country is null)
        {
            return null;
        }

        return new LocationReferenceResponse
        {
            CountryCode = country.Code.Value,
            CountryName = country.Name.Value,
            Flag = country.Flag,
            CityName = scope.CityName,
            Latitude = scope.Center?.Latitude,
            Longitude = scope.Center?.Longitude,
            DisplayName = scope.CityName ?? country.Name.Value,
            CountryId = country.Id.Value
        };
    }
}

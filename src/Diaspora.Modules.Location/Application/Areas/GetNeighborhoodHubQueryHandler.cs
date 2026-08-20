using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>
/// Serves the neighbourhood hub.
/// <para>
/// The one handler that combines all three of the module's halves: the catalogue supplies the
/// place, the counter registry supplies what every vertical has there, and the quality indexes
/// come back nullable because nothing supplies them yet. That last part is the honest version
/// of what the frontend's mock does, which is invent a safety score from the city name.
/// </para>
/// </summary>
public sealed class GetNeighborhoodHubQueryHandler
    : IQueryHandler<GetNeighborhoodHubQuery, NeighborhoodHubResponse>
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;
    private readonly ResourceCountAggregator _counts;

    public GetNeighborhoodHubQueryHandler(
        IAdministrativeAreaRepository areas,
        ICountryRepository countries,
        ResourceCountAggregator counts)
    {
        _areas = areas;
        _countries = countries;
        _counts = counts;
    }

    public async Task<Result<NeighborhoodHubResponse>> Handle(
        GetNeighborhoodHubQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var area = await ResolveAsync(request, cancellationToken);

        if (area is null)
        {
            return Result.Failure<NeighborhoodHubResponse>(LocationErrors.NeighborhoodNotFound);
        }

        var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);

        if (country is null)
        {
            return Result.Failure<NeighborhoodHubResponse>(
                LocationErrors.CountryNotFound(area.CountryCode.Value));
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);
        var city = ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        var counts = await _counts.CountInAreaAsync(area.Id, area.Path, cancellationToken);

        var view = new Neighborhood(area);

        return Result.Success(new NeighborhoodHubResponse
        {
            // The slug, because that is what the client's existing route carries as `id`.
            Id = area.Slug.Value,
            AreaId = area.Id.Value,
            Slug = area.Slug.Value,
            Name = area.Name.Value,
            CityName = city?.Name.Value,
            CountryName = country.Name.Value,
            CountryCode = country.Code.Value,
            Flag = country.Flag,
            Latitude = area.Center?.Latitude,
            Longitude = area.Center?.Longitude,
            Overview = area.Overview,
            Boundary = LocationMapper.ToBoundary(area.Boundary),
            Stats = new NeighborhoodStatsResponse
            {
                SafetyScore = view.SafetyScore,
                Walkability = view.WalkabilityScore,
                CostOfLiving = view.CostOfLiving?.ToString(),
                IsAvailable = view.HasQualityIndexes
            },
            ResourceCounts = counts,
            Ancestors = ancestors
                .Select(ancestor => LocationMapper.ToSummary(
                    ancestor,
                    ancestors.Where(node => node.Path.Contains(ancestor.Path)).ToList()))
                .ToList()
        });
    }

    /// <summary>
    /// Resolves the route parameter, trying an identifier first and then a slug.
    /// <para>
    /// Identifier first because a GUID is unambiguous and a slug is not — and because a slug
    /// that happens to parse as a GUID does not exist.
    /// </para>
    /// </summary>
    private async Task<AdministrativeArea?> ResolveAsync(
        GetNeighborhoodHubQuery request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.IdOrSlug, out var id))
        {
            var byId = await _areas.GetByIdAsync(AreaId.From(id), cancellationToken);

            if (byId?.Level == AdministrativeLevel.Neighborhood)
            {
                return byId;
            }
        }

        var slug = LocationSlug.TryCreate(request.IdOrSlug);

        if (slug.IsFailure)
        {
            return null;
        }

        var countryCode = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : CountryCode.TryCreate(request.CountryCode) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;

        return await _areas.GetBySlugAsync(
            slug.Value, AdministrativeLevel.Neighborhood, countryCode, cancellationToken);
    }
}

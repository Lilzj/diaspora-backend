using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Turns the loose location payload a client sends into a validated snapshot.
/// <para>
/// <b>Coordinates and names, never a catalogue id, since 14 Oct 2026</b> (Phase 2B of retiring the
/// place catalogue). The client describes a place by its Google result: a country, the names, and
/// a point. Those are stored as sent. An <c>areaId</c> from an older app build is used only when
/// the payload names no country at all — to recover the country, names and centre the node knows —
/// and even then the id itself is not stored.
/// </para>
/// </summary>
public sealed class LocationSnapshotFactory
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;

    public LocationSnapshotFactory(
        IAdministrativeAreaRepository areas, ICountryRepository countries)
    {
        _areas = areas;
        _countries = countries;
    }

    public async Task<Result<LocationSnapshot>> CreateAsync(
        SetCurrentLocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // An older client that sent only a node: read the names and point off it, keep no id.
        if (request.AreaId is not null && string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var fromArea = await FromAreaAsync(
                AreaId.From(request.AreaId.Value), cancellationToken);

            if (fromArea is not null)
            {
                return fromArea;
            }
        }

        var code = CountryCode.TryCreate(request.CountryCode);

        if (code.IsFailure)
        {
            return Result.Failure<LocationSnapshot>(code.Error);
        }

        var country = await _countries.GetByCodeAsync(code.Value, cancellationToken);

        if (country is null)
        {
            return Result.Failure<LocationSnapshot>(
                LocationErrors.CountryNotFound(code.Value.Value));
        }

        GeoCoordinate? coordinate = null;

        if (request.Latitude is not null && request.Longitude is not null)
        {
            var parsed = GeoCoordinate.TryCreate(request.Latitude.Value, request.Longitude.Value);

            if (parsed.IsFailure)
            {
                return Result.Failure<LocationSnapshot>(parsed.Error);
            }

            coordinate = parsed.Value;
        }

        return LocationSnapshot.TryCreate(
            country.Code,
            request.CountryName ?? country.Name.Value,
            request.State,
            request.CityName,
            request.Neighborhood,
            request.Street,
            coordinate,
            areaId: null,
            request.DisplayName);
    }

    /// <summary>
    /// Builds a snapshot from a catalogue node and its breadcrumb.
    /// <para>
    /// Returns null rather than failing when the node is gone, so the caller can fall back to
    /// the rest of the payload. A stale area id in a client's cache should not stop a member
    /// from setting their location.
    /// </para>
    /// </summary>
    private async Task<Result<LocationSnapshot>?> FromAreaAsync(
        AreaId areaId, CancellationToken cancellationToken)
    {
        var area = await _areas.GetByIdAsync(areaId, cancellationToken);

        if (area is null)
        {
            return null;
        }

        var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);

        if (country is null)
        {
            return null;
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        var city = area.Level == AdministrativeLevel.City
            ? area
            : ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        var state = ancestors.FirstOrDefault(
            node => node.Level is AdministrativeLevel.StateProvince or AdministrativeLevel.Region);

        var neighborhood = area.Level == AdministrativeLevel.Neighborhood ? area.Name.Value : null;

        return LocationSnapshot.TryCreate(
            country.Code,
            country.Name.Value,
            state?.Name.Value,
            city?.Name.Value,
            neighborhood,
            street: null,
            area.Center,
            areaId: null);
    }
}

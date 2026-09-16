using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// The one class in B11 that reads B4's area catalogue.
/// <para>
/// <b>Every neighbourhood in this module is a real node in Location's hierarchy, and this class is
/// what makes that true rather than aspirational.</b> A client sends an area identifier; this
/// resolves it, confirms it really is at neighbourhood level, and returns the names and the
/// materialised path a listing caches. Nothing else in Housing touches Location's aggregates, so
/// the coupling is one file wide.
/// </para>
/// <para>
/// <b>It also owns the create-on-demand rule for profiles.</b> A neighbourhood gets a profile the
/// first time somebody lists a property in it or says something about it — not when an editor
/// remembers to make one. That ordering is why the platform ends up with profiles for the places
/// members actually live rather than for the places somebody already cared about.
/// </para>
/// <para>
/// <b>Refusing a non-neighbourhood area is the important check.</b> A profile hanging off a city
/// would claim to describe somewhere far larger than anybody wrote about, and a listing placed at
/// city level would never appear in any neighbourhood search.
/// </para>
/// </summary>
public sealed class HousingNeighborhoodResolver
{
    /// <summary>
    /// How far a property may be from a neighbourhood's centre and still be filed in it.
    /// <para>
    /// A catalogue centre is a point standing for an area with no boundary stored (nothing writes
    /// one), so this is the honest substitute for "inside it". Five kilometres files a flat in Rose
    /// Hill under Rose Hill and leaves a house in the cane fields unfiled, which is the right answer
    /// for both.
    /// </para>
    /// </summary>
    private const double NearestNeighborhoodKilometres = 5d;

    /// <summary>How wide a box to ask the catalogue for. Roughly ten kilometres of latitude.</summary>
    private const double CandidateBoxDegrees = 0.09d;

    private readonly IAdministrativeAreaRepository _areas;
    private readonly ILocationDirectory _directory;
    private readonly ICountryRepository _countries;
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public HousingNeighborhoodResolver(
        IAdministrativeAreaRepository areas,
        ILocationDirectory directory,
        ICountryRepository countries,
        INeighborhoodProfileRepository profiles,
        IGuidGenerator guids,
        IClock clock)
    {
        _areas = areas;
        _directory = directory;
        _countries = countries;
        _profiles = profiles;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>
    /// Resolves a client-supplied area into the location context a listing stores.
    /// <para>
    /// A null or empty identifier is not an error: a property may legitimately be placed at city
    /// level while its landlord works out which neighbourhood it is in, and refusing that would
    /// make the neighbourhood a required field on a form nobody can always answer.
    /// </para>
    /// </summary>
    public async Task<Result<HousingLocationContext>> ResolveAsync(
        HousingLocationContext context,
        Guid? areaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (areaId is not { } id || id == Guid.Empty)
        {
            return Result.Success(context);
        }

        var area = await _areas.GetByIdAsync(AreaId.From(id), cancellationToken);

        if (area is null)
        {
            return Result.Failure<HousingLocationContext>(
                HousingErrors.NeighborhoodNotFound);
        }

        if (area.Level != AdministrativeLevel.Neighborhood)
        {
            return Result.Failure<HousingLocationContext>(
                HousingErrors.NeighborhoodAreaInvalid);
        }

        // The country comes from B4's node rather than from what the client typed. A listing whose
        // stated country disagrees with the neighbourhood it names is a listing in the wrong place,
        // and B4 is the authority on which.
        var resolved = context.WithNeighborhood(
            area.Id.Value, area.Path.Value, area.Name.Value);

        return Result.Success(resolved);
    }

    /// <summary>
    /// The profile for an area, creating one if this is the first time anybody has needed it.
    /// <para>
    /// Does not save — the caller owns the unit of work — so the profile commits with whatever
    /// prompted it. A profile created and then abandoned by a failed listing write is a profile
    /// that never existed, which is the correct outcome.
    /// </para>
    /// </summary>
    public async Task<Result<NeighborhoodProfile>> GetOrCreateProfileAsync(
        Guid areaId, UserId? createdBy, CancellationToken cancellationToken = default)
    {
        if (areaId == Guid.Empty)
        {
            return Result.Failure<NeighborhoodProfile>(HousingErrors.NeighborhoodAreaInvalid);
        }

        var existing = await _profiles.GetByAreaAsync(areaId, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(existing);
        }

        var area = await _areas.GetByIdAsync(AreaId.From(areaId), cancellationToken);

        if (area is null)
        {
            return Result.Failure<NeighborhoodProfile>(HousingErrors.NeighborhoodNotFound);
        }

        if (area.Level != AdministrativeLevel.Neighborhood)
        {
            return Result.Failure<NeighborhoodProfile>(
                HousingErrors.NeighborhoodAreaInvalid);
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);
        var city = ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);
        var region = ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.Region);

        var profile = NeighborhoodProfile.Create(
            NeighborhoodProfileId.From(_guids.NewSequentialGuid()),
            area.Id.Value,
            area.CountryCode.Value,
            area.Name.Value,
            area.Slug.Value,
            _clock.UtcNow,
            createdBy,
            city?.Name.Value,
            region?.Name.Value,
            area.Path.Value);

        if (profile.IsFailure)
        {
            return profile;
        }

        await _profiles.AddAsync(profile.Value, cancellationToken);

        return profile;
    }

    /// <summary>
    /// Refreshes a profile's cached geography from B4.
    /// <para>
    /// Called when a place is renamed or moved. Deliberately not a content edit: a neighbourhood
    /// being renamed says nothing about whether the prose describing it is still true, so it does
    /// not unapprove the profile.
    /// </para>
    /// </summary>
    public async Task RefreshPlaceAsync(
        NeighborhoodProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var area = await _areas.GetByIdAsync(AreaId.From(profile.AreaId), cancellationToken);

        if (area is null)
        {
            return;
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        profile.RefreshPlace(
            area.Name.Value,
            area.Slug.Value,
            ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City)?.Name.Value,
            ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.Region)?.Name.Value,
            area.Path.Value);
    }

    /// <summary>
    /// Whether a country code names a country B4 knows about.
    /// <para>
    /// Checked on landlord registration and on any listing that names no neighbourhood, so a
    /// property is never filed under a country that does not exist.
    /// </para>
    /// </summary>
    /// <summary>
    /// The neighbourhood a property's own coordinates fall in, when the client named none.
    /// <para>
    /// <b>No listing on this platform has ever been filed under an area.</b> A neighbourhood was
    /// only ever resolved from a <c>neighborhoodAreaId</c> the client sent, and no create screen
    /// sends one — so every listing carried a country, a city name and nothing the catalogue knows.
    /// The consequences were not cosmetic: a town search by path matched nothing (DEFECT-010's
    /// other half), every neighbourhood's resource count read zero, and the map's cluster endpoint
    /// aggregated a catalogue of noughts.
    /// </para>
    /// <para>
    /// The address is the authority. Candidates come from the catalogue box around the point,
    /// narrowed to neighbourhood level in the same country, and the nearest centre within
    /// <see cref="NearestNeighborhoodKilometres"/> wins — measured by Location, because every
    /// distance on this platform is. A property with no coordinates, or none near a known
    /// neighbourhood, stays unfiled rather than being pushed into the closest thing on the list.
    /// </para>
    /// </summary>
    public async Task<HousingLocationContext> ResolveFromPointAsync(
        HousingLocationContext context,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HasNeighborhood
            || latitude is not { } lat
            || longitude is not { } lng
            || GeoCoordinate.TryCreate(lat, lng) is not { IsSuccess: true } point)
        {
            return context;
        }

        var box = BoundingBox.TryCreate(
            north: lat + CandidateBoxDegrees,
            south: lat - CandidateBoxDegrees,
            east: lng + CandidateBoxDegrees,
            west: lng - CandidateBoxDegrees);

        if (box.IsFailure)
        {
            return context;
        }

        var country = CountryCode.TryCreate(context.CountryCode);

        var candidates = await _areas.GetWithinBoundsAsync(
            box.Value,
            [AdministrativeLevel.Neighborhood],
            country.IsSuccess ? country.Value : null,
            withinPath: null,
            limit: CandidateLimit,
            cancellationToken);

        var nearest = candidates
            .Where(area => area.Center is not null)
            .Select(area => (Area: area,
                Kilometres: _directory.DistanceBetween(point.Value, area.Center!).Kilometers))
            .Where(measured => measured.Kilometres <= NearestNeighborhoodKilometres)
            .OrderBy(measured => measured.Kilometres)
            .ThenBy(measured => measured.Area.Slug.Value, StringComparer.Ordinal)
            .Select(measured => measured.Area)
            .FirstOrDefault();

        return nearest is null
            ? context
            : context.WithNeighborhood(nearest.Id.Value, nearest.Path.Value, nearest.Name.Value);
    }

    /// <summary>How many catalogue nodes a point's box may return before the nearest is chosen.</summary>
    private const int CandidateLimit = 50;

    public async Task<bool> CountryExistsAsync(
        string? countryCode, CancellationToken cancellationToken = default)
    {
        var normalized = HousingLocationContext.NormalizeCountry(countryCode);

        if (normalized is null)
        {
            return false;
        }

        // Fully qualified: both Location and the platform Domain declare a CountryCode, and the
        // repository takes Location's. An unqualified name here resolves to whichever using
        // directive happens to come first, which is a bug that compiles.
        var parsed = Modules.Location.Domain.ValueObjects.CountryCode.TryCreate(normalized);

        return parsed.IsSuccess
            && await _countries.GetByCodeAsync(parsed.Value, cancellationToken) is not null;
    }
}

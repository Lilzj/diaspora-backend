using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Checks a listing's accommodation type and amenities against the catalogues.
/// <para>
/// <b>The catalogues are configurable, so this check cannot be a compile-time one.</b> A
/// deployment that added "Co-living Suite" or "Borehole" needs them accepted; a client that sent a
/// typo needs it refused. The catalogue is the authority in both cases, and this is the single
/// place that asks it.
/// </para>
/// <para>
/// <b>Retired entries are refused on write and kept on read.</b> A landlord cannot newly claim an
/// amenity the deployment has retired, and every listing that already claims it still renders it —
/// because the property still has the thing, and blanking it would silently misdescribe a home.
/// </para>
/// <para>
/// Scoped, and it reads the amenity key set once per request. The validator asks it once per
/// amenity and a serviced apartment sends forty.
/// </para>
/// </summary>
public sealed class HousingCatalogResolver
{
    private readonly IAccommodationTypeRepository _types;
    private readonly IHousingAmenityRepository _amenities;

    private IReadOnlySet<string>? _activeAmenityKeys;

    public HousingCatalogResolver(
        IAccommodationTypeRepository types, IHousingAmenityRepository amenities)
    {
        _types = types;
        _amenities = amenities;
    }

    /// <summary>
    /// Resolves an accommodation-type key to its catalogue entry, refusing an unknown or retired
    /// one.
    /// </summary>
    public async Task<Result<AccommodationType>> RequireTypeAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var normalized = HousingListing.NormalizeTypeKey(key);

        if (normalized is null)
        {
            return Result.Failure<AccommodationType>(HousingErrors.AccommodationTypeInvalid);
        }

        var type = await _types.GetByKeyAsync(normalized, cancellationToken);

        if (type is null)
        {
            return Result.Failure<AccommodationType>(HousingErrors.AccommodationTypeNotFound);
        }

        return type.IsActive
            ? Result.Success(type)
            : Result.Failure<AccommodationType>(HousingErrors.AccommodationTypeInactive);
    }

    /// <summary>
    /// Turns requested amenities into assignments, refusing any the catalogue does not know.
    /// <para>
    /// Duplicates are collapsed rather than refused: a client sending the same amenity twice meant
    /// it once, and an error would be a validation message about a mistake nobody made on purpose.
    /// </para>
    /// </summary>
    public async Task<Result<IReadOnlyList<HousingAmenityAssignment>>> ResolveAmenitiesAsync(
        IReadOnlyList<HousingAmenityRequest>? requested,
        CancellationToken cancellationToken = default)
    {
        if (requested is null || requested.Count == 0)
        {
            return Result.Success<IReadOnlyList<HousingAmenityAssignment>>([]);
        }

        if (requested.Count > HousingLimits.MaxAmenities)
        {
            return Result.Failure<IReadOnlyList<HousingAmenityAssignment>>(
                HousingErrors.AmenityLimit);
        }

        var known = await ActiveAmenityKeysAsync(cancellationToken);
        var assignments = new List<HousingAmenityAssignment>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var amenity in requested)
        {
            var normalized = HousingAmenityAssignment.NormalizeKey(amenity.Key);

            if (normalized is null)
            {
                return Result.Failure<IReadOnlyList<HousingAmenityAssignment>>(
                    HousingErrors.AmenityInvalid);
            }

            if (!known.Contains(normalized))
            {
                return Result.Failure<IReadOnlyList<HousingAmenityAssignment>>(
                    HousingErrors.AmenityNotFound);
            }

            if (!seen.Add(normalized))
            {
                continue;
            }

            assignments.Add(HousingAmenityAssignment.Create(normalized, amenity.Note));
        }

        return Result.Success<IReadOnlyList<HousingAmenityAssignment>>(assignments);
    }

    /// <summary>
    /// The amenity catalogue as a lookup, for a mapper resolving keys to names.
    /// <para>
    /// Includes retired entries, deliberately: a listing that claims a retired amenity should still
    /// render its name rather than falling back to a raw key.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, HousingAmenity>> AmenityLookupAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _amenities.GetAllAsync(includeInactive: true, cancellationToken);

        return all.ToDictionary(amenity => amenity.Key, StringComparer.Ordinal);
    }

    /// <summary>The accommodation-type catalogue as a lookup, including retired entries.</summary>
    public async Task<IReadOnlyDictionary<string, AccommodationType>> TypeLookupAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _types.GetAllAsync(includeInactive: true, cancellationToken);

        return all.ToDictionary(type => type.Key, StringComparer.Ordinal);
    }

    private async Task<IReadOnlySet<string>> ActiveAmenityKeysAsync(
        CancellationToken cancellationToken) =>
        _activeAmenityKeys ??= await _amenities.GetActiveKeysAsync(cancellationToken);
}

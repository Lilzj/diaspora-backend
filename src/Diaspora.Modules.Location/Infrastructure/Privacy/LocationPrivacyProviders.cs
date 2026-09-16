using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Persistence;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Infrastructure.Privacy;

/// <summary>
/// The module's name in a privacy receipt, and the two data sets it holds about a member.
/// </summary>
internal static class LocationPrivacy
{
    public const string Module = "Location";

    public const string ProfileDataSet = "location profile";

    public const string SavedPlacesDataSet = "saved places";
}

/// <summary>
/// Erases where a member is, where they have been, and the places they saved.
/// <para>
/// <b>Written before the client was allowed to save one.</b> The maps register refused server-side
/// saved places outright — "a home address is the most sensitive field this application would ever
/// persist server-side, and it must not be created without a retention policy, an access rule and a
/// deletion path specified first" — and it was right: the routes existed, and the platform held
/// exactly one erasure provider, Identity's, which reaches sessions, devices and codes and nothing
/// of this. Closing an account left a home address behind.
/// </para>
/// <para>
/// <b>Deleted, never anonymised.</b> A saved place is a coordinate and a label the member chose; an
/// anonymised copy of "Home" is still a home address with a different name on it. The whole profile
/// goes, saved places and recents with it as owned collections, because none of it is anybody
/// else's record and nothing on the platform points at it.
/// </para>
/// </summary>
public sealed class LocationDataErasureProvider : IDataErasureProvider
{
    private readonly DiasporaDbContext _dbContext;

    public LocationDataErasureProvider(DiasporaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public string Module => LocationPrivacy.Module;

    public async Task<IReadOnlyList<ErasureResult>> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await FindAsync(userId, cancellationToken);

        if (profile is null)
        {
            return [new ErasureResult(Module, LocationPrivacy.ProfileDataSet, ErasureOutcome.NothingHeld, 0)];
        }

        return
        [
            new ErasureResult(Module, LocationPrivacy.ProfileDataSet, ErasureOutcome.Deleted, 1),
            new ErasureResult(
                Module,
                LocationPrivacy.SavedPlacesDataSet,
                ErasureOutcome.Deleted,
                profile.SavedLocations.Count + profile.RecentLocations.Count),
        ];
    }

    public async Task<IReadOnlyList<ErasureResult>> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await FindAsync(userId, cancellationToken);

        if (profile is null)
        {
            return [new ErasureResult(Module, LocationPrivacy.ProfileDataSet, ErasureOutcome.NothingHeld, 0)];
        }

        var places = profile.SavedLocations.Count + profile.RecentLocations.Count;

        // The collections are owned by the profile, so removing the aggregate takes them with it —
        // and does so in one statement rather than leaving a window where a member's places
        // outlive the profile that gates access to them.
        _dbContext.Set<LocationProfile>().Remove(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return
        [
            new ErasureResult(Module, LocationPrivacy.ProfileDataSet, ErasureOutcome.Deleted, 1),
            new ErasureResult(Module, LocationPrivacy.SavedPlacesDataSet, ErasureOutcome.Deleted, places),
        ];
    }

    private Task<LocationProfile?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Set<LocationProfile>()
            .IgnoreQueryFilters()
            .Include(profile => profile.SavedLocations)
            .Include(profile => profile.RecentLocations)
            .FirstOrDefaultAsync(profile => profile.UserId == UserId.From(userId), cancellationToken);
}

/// <summary>
/// Gives a member back the places they saved.
/// <para>
/// What they contributed, which is the line the export contract draws: the label they chose and the
/// place they chose it for. Not the areas the catalogue resolved around them, and not the distances
/// anything computed — those are the platform's opinions, not their data.
/// </para>
/// </summary>
public sealed class LocationDataExportProvider : IDataExportProvider
{
    private readonly DiasporaDbContext _dbContext;

    public LocationDataExportProvider(DiasporaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public string Module => LocationPrivacy.Module;

    public async Task<IReadOnlyList<ExportSection>> ExportAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Set<LocationProfile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(entity => entity.SavedLocations)
            .FirstOrDefaultAsync(entity => entity.UserId == UserId.From(userId), cancellationToken);

        if (profile is null)
        {
            return [];
        }

        var places = profile.SavedLocations
            .Select(place => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = place.Label,
                ["countryCode"] = place.Location.CountryCode.Value,
                ["cityName"] = place.Location.CityName,
                ["neighborhood"] = place.Location.Neighborhood,
                ["latitude"] = place.Location.Coordinate?.Latitude,
                ["longitude"] = place.Location.Coordinate?.Longitude,
                ["savedAt"] = place.SavedAt,
            })
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToList();

        return
        [
            new ExportSection(Module, LocationPrivacy.SavedPlacesDataSet, places),
        ];
    }
}

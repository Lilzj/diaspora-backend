using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Infrastructure.Location;

/// <summary>
/// Contributes the provider count to Location's neighbourhood hub and country page.
/// <para>
/// The client's neighbourhood screen renders six numbers, one of which is providers, and its
/// mock computes them by filtering a local array. This is the seam that replaces that without
/// Location learning that B7 exists: <c>providers</c> is one of the six
/// <c>resourceCounts</c> keys B4's contract freeze already published.
/// </para>
/// <para>
/// Approximate is acceptable and expected — B4's own interface says so. These are headline
/// numbers on a browse screen, not a ledger, and an exact count that makes the hub slow is a
/// worse trade than one that is briefly stale.
/// </para>
/// </summary>
public sealed class ProviderResourceCounter : ILocationResourceCounter
{
    /// <summary>One of the six keys the client's <c>resourceCounts</c> already expects.</summary>
    public const string Key = "providers";

    private readonly IProviderRepository _providers;

    public ProviderResourceCounter(IProviderRepository providers) => _providers = providers;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        return await _providers.CountInAreaAsync(path.Value, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _providers.CountInCountryAsync(countryCode.Value, cancellationToken);
    }

    /// <summary>
    /// Where the active public providers sit inside a viewport, by their own location's pin
    /// (14 Oct 2026). A provider with no pin (a remote one, or one that never set it) is not on
    /// the map, and service areas do not widen the box.
    /// </summary>
    public async Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, MapPointReader.MaximumRowsRead);

        var candidates = await _providers.QueryNearestCandidatesAsync(
            new ProviderQueryFilter
            {
                Statuses = [ProviderStatus.Active],
                AllowedVisibility = [ProviderVisibilityLevel.Public],
                CountryCode = query.CountryCode,
                MinLatitude = query.South,
                MaxLatitude = query.North,
                MinLongitude = query.West,
                MaxLongitude = query.East,
                IncludeRemoteProviders = false,
                MatchServiceAreas = false
            },
            limit + 1,
            cancellationToken);

        return MapPointReader.FromList(
            query, candidates, candidate => (candidate.Latitude, candidate.Longitude));
    }
}

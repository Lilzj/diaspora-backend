using System.Globalization;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Infrastructure.Location;

/// <summary>
/// Contributes providers to Location's <c>GET /nearby</c>.
/// <para>
/// The resource type is <c>providers</c>, which is one of the six keys Location's own contract
/// freeze published — B7 claims a key that was reserved for it rather than inventing a seventh.
/// This is the whole of what B7 adds to Location: one class, in this module, and nothing at all
/// in that one.
/// </para>
/// <para>
/// <b>Every distance in the response comes from Location.</b> This provider narrows by country
/// and area, hands back items, and lets the caller that owns the geometry do the geometry. B7
/// computing its own haversine here would produce a second, subtly different distance in the
/// same response as B4's — and the two would disagree at the fourth decimal place in a way
/// nobody could reproduce.
/// </para>
/// </summary>
public sealed class ProviderNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>One of the six resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "providers";

    private readonly IProviderRepository _providers;
    private readonly ILocationDirectory _location;

    public ProviderNearbyResourceProvider(
        IProviderRepository providers, ILocationDirectory location)
    {
        _providers = providers;
        _location = location;
    }

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new ProviderQueryFilter
        {
            Statuses = [ProviderStatus.Active],

            // Nearby is a public surface. Only public providers are contributed, because
            // Location's nearby endpoint has no notion of a viewer's audience and B7 must not
            // assume one on its behalf.
            AllowedVisibility = [ProviderVisibilityLevel.Public],
            CountryCode = request.CountryCode.Value,
            AreaId = request.AreaId?.Value,
            HierarchyPathPrefix = request.WithinPath?.Value,
            CityName = request.CityName,
            Sort = ProviderSortOrder.Rating,
            Limit = Math.Min(request.Limit, ProviderLimits.MaxNearbyResults)
        };

        var page = await _providers.QueryAsync(filter, cancellationToken);

        var origin = request.Search.Center;

        var items = page.Items
            .Select(provider => new NearbyResourceItem
            {
                Id = provider.Id.Value.ToString(),
                Title = provider.Name.Value,
                Category = provider.CategoryId.Value.ToString(),
                DistanceKm = provider.Location.HasCoordinates
                    ? _location.DistanceBetween(
                        origin,
                        Modules.Location.Domain.ValueObjects.GeoCoordinate.Create(
                            provider.Location.Latitude!.Value,
                            provider.Location.Longitude!.Value)).Kilometers
                    : 0d,
                Latitude = provider.Location.Latitude,
                Longitude = provider.Location.Longitude,
                CityName = provider.Location.CityName,
                AreaName = provider.Location.AreaName,
                Rating = provider.ReviewSummary.HasReviews ? provider.ReviewSummary.Average : null,
                Payload = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["slug"] = provider.Slug.Value,
                    ["providerType"] = ProviderWireNames.ToWire(provider.Type),
                    ["isVerified"] = provider.IsVerified
                        ? bool.TrueString
                        : bool.FalseString,
                    ["reviewCount"] = provider.ReviewSummary.TotalReviews
                        .ToString(CultureInfo.InvariantCulture)
                }
            })
            .OrderBy(item => item.DistanceKm)
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }
}

using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// Content near a coordinate.
/// <para>
/// Refuses to serve without coordinates rather than quietly returning everything. A nearby feed
/// that silently degrades to a global one is indistinguishable, from the client's side, from a
/// very busy neighbourhood.
/// </para>
/// <para>
/// The bounding box is a <b>prefilter</b>. It narrows candidate rows to a rectangle so Location
/// is asked for real distances about tens of posts rather than tens of thousands; the ordering
/// that a member sees comes from Location's distances via the location-weighted ranking, never
/// from the rectangle. A rectangle is not a circle and B6 does not pretend otherwise.
/// </para>
/// </summary>
public sealed class NearbyFeedProvider : PostFeedProvider
{
    /// <summary>Kilometres per degree of latitude. The one constant the prefilter needs.</summary>
    private const double KilometresPerDegreeLatitude = 111.32d;

    private const double DefaultRadiusKm = 25d;

    public NearbyFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Nearby;

    public override string DefaultRankingKey => RankingKeys.LocationWeighted;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No catalogue area (14 Oct 2026): a point or a typed town.
        return request.HasCoordinates || !string.IsNullOrWhiteSpace(request.CityName);
    }

    /// <summary>
    /// A circle is the rectangle around it over the posts' own coordinates, with unpinned posts in
    /// the town the request named; a town- or country-wide search (a negative radius sentinel) is
    /// the town's name and the country. Never the catalogue node the point is nearest — until
    /// 14 Oct 2026 a feed with coordinates was narrowed to exactly that node's area id, so a post
    /// pinned in the next street, filed against a different node or none, was not "nearby".
    /// </summary>
    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request)
    {
        var aroundPoint = request.HasCoordinates && request.RadiusKm is not < 0;

        var narrowed = filter with
        {
            CountryCode = request.CountryCode,
            AreaId = null,
            HierarchyPathPrefix = null,
            CityName = aroundPoint ? null : request.CityName
        };

        if (!aroundPoint)
        {
            return narrowed;
        }

        if (request.BoundingBox is { } resolved)
        {
            return narrowed with { BoundingBox = resolved, UnpinnedCityName = request.CityName };
        }

        var radiusKm = request.RadiusKm is > 0 ? request.RadiusKm.Value : DefaultRadiusKm;
        var latitude = request.Latitude!.Value;
        var longitude = request.Longitude!.Value;

        var latitudeDelta = radiusKm / KilometresPerDegreeLatitude;

        // Longitude degrees shrink toward the poles. Without the cosine the box is far too
        // narrow in Lagos and far too wide in Reykjavik, and posts go missing at one end.
        var longitudeScale = Math.Max(0.01d, Math.Cos(latitude * Math.PI / 180d));
        var longitudeDelta = radiusKm / (KilometresPerDegreeLatitude * longitudeScale);

        return narrowed with
        {
            UnpinnedCityName = request.CityName,
            BoundingBox = (
                latitude - latitudeDelta,
                longitude - longitudeDelta,
                latitude + latitudeDelta,
                longitude + longitudeDelta)
        };
    }

}

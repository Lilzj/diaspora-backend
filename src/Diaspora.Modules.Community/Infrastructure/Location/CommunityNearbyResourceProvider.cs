using System.Globalization;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Integration;

namespace Diaspora.Modules.Community.Infrastructure.Location;

/// <summary>
/// Contributes community posts to Location's <c>GET /nearby</c>.
/// <para>
/// The resource type is <c>updates</c>, which is one of the six keys Location's own contract
/// freeze publishes — B6 claims a key that was reserved for it rather than inventing a seventh.
/// This is the whole of what B6 adds to Location: one class, in this module, and nothing at all
/// in that one.
/// </para>
/// <para>
/// Every distance in the response comes from Location. This provider narrows by country and
/// area, hands back items, and lets the caller that owns the geometry do the geometry.
/// </para>
/// </summary>
public sealed class CommunityNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>One of the six resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "updates";

    private readonly ICommunityPostRepository _posts;
    private readonly ILocationDirectory _location;

    public CommunityNearbyResourceProvider(
        ICommunityPostRepository posts, ILocationDirectory location)
    {
        _posts = posts;
        _location = location;
    }

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Within a circle, the circle is the question — 16 Sep 2026 ──────────────────────
        //
        // This narrowed by the catalogue node the point resolved to: the area id, its path and
        // its city. The resolver snaps a coordinate to the nearest node, and an update carries
        // an area only if whoever posted it happened to supply one — so "updates within 5 km"
        // asked for "updates filed against Peckham", and a report pinned to the next street with
        // no area id was not near anybody. B11 met the same defect in housing search on 15 Sep;
        // this is the same fix, in the module that owns the content.
        //
        // The rectangle around the circle is an indexed prefilter; the circle itself is applied
        // below, against the same distance the item carries.
        var circle = request.Search.Radius.Scope == RadiusScope.Radius;

        var filter = new PostQueryFilter
        {
            Statuses = [PostStatus.Published],

            // Nearby is a public surface. Only public content is contributed, because Location's
            // nearby endpoint has no notion of a viewer's audience and B6 must not assume one.
            AllowedVisibility = [VisibilityLevel.Public],
            CountryCode = request.CountryCode.Value,

            // A city- or country-wide search is a hierarchy question and keeps the node narrowing.
            AreaId = circle ? null : request.AreaId?.Value,
            HierarchyPathPrefix = circle ? null : request.WithinPath?.Value,
            CityName = circle ? null : request.CityName,
            BoundingBox = circle
                ? (request.Search.Bounds.South, request.Search.Bounds.West,
                    request.Search.Bounds.North, request.Search.Bounds.East)
                : null,
            Limit = Math.Min(request.Limit, Domain.Policies.CommunityLimits.MaxFeedPageSize)
        };

        var page = await _posts.QueryAsync(filter, cancellationToken);

        IEnumerable<Domain.Posts.CommunityPost> candidates = page.Items;

        // ── An alert reaches as far as its author said — 16 Sep 2026 ──────────────────────────
        //
        // A post that declared an affected area is near anybody inside that area, even when its pin
        // is outside the circle they searched: a flood reported 8 km away with a 10 km radius covers
        // the member searching 5 km around their home. Asked as a second query, over the rectangle
        // grown by the largest radius a post may declare, for posts that declared one — so the common
        // query stays exactly as narrow as it was, and the wide one only ever finds circles.
        if (circle)
        {
            var grown = GrowBy(request.Search.Bounds, CommunityLimits.MaxAffectedRadiusMetres / 1000d);

            var reaching = await _posts.QueryAsync(
                filter with { BoundingBox = grown, HasAffectedArea = true }, cancellationToken);

            candidates = candidates.Concat(reaching.Items).DistinctBy(post => post.Id);
        }

        var origin = request.Search.Center;
        var searchedKm = request.Search.Radius.Distance.Kilometers;

        var items = candidates
            .Select(post => (
                Post: post,
                DistanceKm: post.Location.HasCoordinates
                    ? _location.DistanceBetween(
                        origin,
                        Modules.Location.Domain.ValueObjects.GeoCoordinate.Create(
                            post.Location.Latitude!.Value, post.Location.Longitude!.Value)).Kilometers
                    : 0d))
            // The corners of the rectangle are not within the circle. Trimmed against the radius the
            // member asked for, widened by however far the post itself reaches: two circles overlap
            // when their centres are closer than their radii added together.
            .Where(measured => !circle
                || measured.DistanceKm <= searchedKm + (measured.Post.AffectedRadiusMetres ?? 0) / 1000d)
            .Select(measured => new NearbyResourceItem
            {
                Id = measured.Post.Id.Value.ToString(),
                Title = measured.Post.Title ?? measured.Post.Body.Value,
                Category = CommunityWireNames.ToWire(measured.Post.Category),
                DistanceKm = measured.DistanceKm,
                Latitude = measured.Post.Location.Latitude,
                Longitude = measured.Post.Location.Longitude,
                CityName = measured.Post.Location.CityName,
                AreaName = measured.Post.Location.AreaName,
                // What a map alert renders beyond a pin: its own words, when it was
                // reported, and when it stops being true. The sweep moves a post to
                // Expired and this provider asks for Published only, so the expiry is
                // for the minutes between — a client must be able to drop an alert
                // whose time has passed rather than draw it until the sweep runs.
                Payload = Payload(measured.Post)
            })
            .OrderBy(item => item.DistanceKm)
            .Take(filter.Limit)
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }

    /// <summary>
    /// The fields a community update's own client needs, which Location is opaque to.
    /// <para>
    /// Only what a surface renders: the body under the headline, the "reported 20 minutes ago"
    /// line, the expiry a map applies, and the country a share link states. Absent values are
    /// left out rather than written as empty strings, so a client can tell "not said" from "".
    /// </para>
    /// </summary>
    private static Dictionary<string, string> Payload(Domain.Posts.CommunityPost post)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["urgency"] = CommunityWireNames.ToWire(post.Urgency),
            ["verificationStatus"] = CommunityWireNames.ToWire(post.VerificationStatus),
            ["confirmationCount"] = post.ConfirmationCount.ToString(CultureInfo.InvariantCulture),
            ["status"] = CommunityWireNames.ToWire(post.Status),
            ["body"] = post.Body.Value
        };

        if (post.PublishedAt is { } publishedAt)
        {
            payload["publishedAt"] = publishedAt.ToString("O", CultureInfo.InvariantCulture);
        }

        if (post.ExpiresAt is { } expiresAt)
        {
            payload["expiresAt"] = expiresAt.ToString("O", CultureInfo.InvariantCulture);
        }

        if (post.Location.CountryCode is { Length: > 0 } countryCode)
        {
            payload["countryCode"] = countryCode;
        }

        // "exact" when its author pinned it, "area" when it sits at the centre of the place they
        // named — which a map should draw as approximate, not as a precise spot.
        if (post.Location.HasCoordinates)
        {
            payload["locationPrecision"] = post.Location.PointIsExact ? "exact" : "area";
        }

        // The circle the map draws, and nothing wider: absent means the alert is at its pin.
        if (post.AffectedRadiusMetres is { } radius)
        {
            payload["affectedRadiusMetres"] = radius.ToString(CultureInfo.InvariantCulture);
        }

        // An amended alert says so. Inside the silent window an edit is not marked, and the
        // payload follows the same rule as the post itself.
        if (post.IsEdited && post.EditedAt is { } editedAt)
        {
            payload["editedAt"] = editedAt.ToString("O", CultureInfo.InvariantCulture);
        }

        return payload;
    }

    /// <summary>
    /// The rectangle grown outward by <paramref name="kilometres"/> on every side.
    /// <para>
    /// Longitude degrees shrink toward the poles, so the east-west growth is divided by the cosine
    /// of the latitude nearest a pole, with a floor so it cannot run away there. Over-reaching is
    /// harmless: the exact circle test follows.
    /// </para>
    /// </summary>
    private static (double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)
        GrowBy(Modules.Location.Domain.ValueObjects.BoundingBox bounds, double kilometres)
    {
        const double kilometresPerDegree = 111d;

        var latitudeGrowth = kilometres / kilometresPerDegree;
        var widest = Math.Max(Math.Abs(bounds.North), Math.Abs(bounds.South));
        var shrink = Math.Max(Math.Cos(widest * Math.PI / 180d), 0.2d);
        var longitudeGrowth = latitudeGrowth / shrink;

        return (
            Math.Max(bounds.South - latitudeGrowth, -90d),
            Math.Max(bounds.West - longitudeGrowth, -180d),
            Math.Min(bounds.North + latitudeGrowth, 90d),
            Math.Min(bounds.East + longitudeGrowth, 180d));
    }
}

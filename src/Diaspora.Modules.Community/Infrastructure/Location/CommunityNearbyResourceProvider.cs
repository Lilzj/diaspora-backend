using System.Globalization;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Location.Extensibility;
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

        var filter = new PostQueryFilter
        {
            Statuses = [PostStatus.Published],

            // Nearby is a public surface. Only public content is contributed, because Location's
            // nearby endpoint has no notion of a viewer's audience and B6 must not assume one.
            AllowedVisibility = [VisibilityLevel.Public],
            CountryCode = request.CountryCode.Value,
            AreaId = request.AreaId?.Value,
            HierarchyPathPrefix = request.WithinPath?.Value,
            CityName = request.CityName,
            Limit = Math.Min(request.Limit, Domain.Policies.CommunityLimits.MaxFeedPageSize)
        };

        var page = await _posts.QueryAsync(filter, cancellationToken);

        var origin = request.Search.Center;

        var items = page.Items
            .Select(post => new NearbyResourceItem
            {
                Id = post.Id.Value.ToString(),
                Title = post.Title ?? post.Body.Value,
                Category = CommunityWireNames.ToWire(post.Category),
                DistanceKm = post.Location.HasCoordinates
                    ? _location.DistanceBetween(
                        origin,
                        Modules.Location.Domain.ValueObjects.GeoCoordinate.Create(
                            post.Location.Latitude!.Value, post.Location.Longitude!.Value)).Kilometers
                    : 0d,
                Latitude = post.Location.Latitude,
                Longitude = post.Location.Longitude,
                CityName = post.Location.CityName,
                AreaName = post.Location.AreaName,
                // What a map alert renders beyond a pin: its own words, when it was
                // reported, and when it stops being true. The sweep moves a post to
                // Expired and this provider asks for Published only, so the expiry is
                // for the minutes between — a client must be able to drop an alert
                // whose time has passed rather than draw it until the sweep runs.
                Payload = Payload(post)
            })
            .OrderBy(item => item.DistanceKm)
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

        return payload;
    }
}

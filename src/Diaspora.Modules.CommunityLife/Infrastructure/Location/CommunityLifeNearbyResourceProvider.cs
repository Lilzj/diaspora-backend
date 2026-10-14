using System.Globalization;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Location;

/// <summary>
/// What B12 contributes to Location's "what is near me" screen.
/// <para>
/// <b>Events rather than organizations, and soonest first.</b> Somebody standing in a place asking
/// what is around them is asking what is happening, not which institutions have addresses there —
/// and an organization page is something they find by searching for it rather than by walking past.
/// </para>
/// <para>
/// <b>No viewer is supplied, deliberately.</b> Nearby is a public surface and B12 must not assume a
/// caller's audience on Location's behalf, so no draft, unlisted or members-only event can reach it.
/// </para>
/// <para>
/// <b>The distance is zero because this is not a distance search.</b> Location asked for what is in
/// an area; the events come back filtered by that area, not sorted by metres. Inventing a number
/// here would be a figure a client could sort by and would be wrong.
/// </para>
/// </summary>
public sealed class CommunityLifeNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>
    /// <c>events</c>, one of the six keys Location reserved and the one the client's existing mock
    /// already renders. A new name here would have produced a group the app silently dropped.
    /// </summary>
    public const string Key = "events";

    private readonly ICommunityEventRepository _events;
    private readonly IClock _clock;

    public CommunityLifeNearbyResourceProvider(ICommunityEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No catalogue node (14 Oct 2026). A circle is the rectangle around it over venue
        // coordinates, with venue-less events kept when they name the requested town; a town- or
        // country-wide search is the town's name and the country.
        var circle = request.IsCircle ? request.Search.Bounds : null;

        var page = await _events.SearchAsync(
            new EventQueryFilter
            {
                CountryCode = request.CountryCode?.Value,
                CityName = circle is null ? request.CityName : null,
                UnpinnedCityName = circle is null ? null : request.CityName,
                BoundsSouth = circle?.South,
                BoundsNorth = circle?.North,
                BoundsWest = circle?.West,
                BoundsEast = circle?.East,
                Statuses = [EventStatus.Published, EventStatus.InProgress],
                StartsAfter = _clock.UtcNow,
                IncludeNonPublic = false,
                IncludePast = false,
                SortOrder = EventSortOrder.Soonest,
                Limit = Math.Clamp(request.Limit, 1, CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        var items = page.Items
            .Select(communityEvent => new NearbyResourceItem
            {
                Id = communityEvent.Id.Value.ToString(),
                Title = communityEvent.Title.Value,
                Category = communityEvent.CategoryKey,

                // Not a distance. See the class summary.
                DistanceKm = 0d,
                CityName = communityEvent.Location.CityName,
                AreaName = communityEvent.Location.NeighborhoodName
                    ?? communityEvent.Location.RegionName,
                Payload = BuildPayload(communityEvent)
            })
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }

    private static Dictionary<string, string> BuildPayload(CommunityEvent communityEvent)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["slug"] = communityEvent.Slug.Value,
            ["categoryKey"] = communityEvent.CategoryKey,
            ["startsAt"] = communityEvent.Schedule.StartsAt.ToString(
                "O", CultureInfo.InvariantCulture),
            ["attendanceMode"] = communityEvent.AttendanceMode.ToString(),
            ["isFree"] = communityEvent.Audience.IsFree
                ? bool.TrueString
                : bool.FalseString,
            ["goingCount"] = communityEvent.Capacity.GoingCount.ToString(
                CultureInfo.InvariantCulture)
        };

        if (communityEvent.Venue.Name is { } venueName)
        {
            payload["venueName"] = venueName;
        }

        if (communityEvent.CoverImage?.MediaId is { } coverMediaId)
        {
            payload["coverMediaId"] = coverMediaId.ToString();
        }

        if (communityEvent.Capacity.RemainingPlaces is { } remaining)
        {
            payload["remainingPlaces"] = remaining.ToString(CultureInfo.InvariantCulture);
        }

        return payload;
    }
}

using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Location;

/// <summary>
/// How many events B12 has in a place, for Location's area and country pages.
/// <para>
/// <b>Counted by materialised path rather than by area identifier</b>, so a city returns everything
/// in its neighbourhoods rather than only what was filed directly against the city itself. That is
/// B4's whole reason for keeping a path.
/// </para>
/// <para>
/// <b>Upcoming only.</b> A number on an area page is read as "there is something happening here",
/// and counting fifteen years of finished events would make every place look busy and none of them
/// distinguishable.
/// </para>
/// </summary>
public sealed class CommunityLifeResourceCounter : ILocationResourceCounter
{
    /// <summary>The same key the nearby provider claims. See its remarks.</summary>
    public const string Key = "events";

    private readonly ICommunityEventRepository _events;
    private readonly IClock _clock;

    public CommunityLifeResourceCounter(ICommunityEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        return await _events.CountInAreaAsync(path.Value, _clock.UtcNow, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _events.CountInCountryAsync(
            countryCode.Value, _clock.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Where the public, not-yet-finished events sit inside a viewport, by their venue's
    /// coordinates (14 Oct 2026). An online event, or one whose venue has no pin, is not on the map.
    /// </summary>
    public Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new EventQueryFilter
        {
            CountryCode = query.CountryCode,
            BoundsSouth = query.South,
            BoundsNorth = query.North,
            BoundsWest = query.West,
            BoundsEast = query.East,
            Statuses = [EventStatus.Published, EventStatus.InProgress],
            IncludeNonPublic = false,
            IncludePast = false,
            SortOrder = EventSortOrder.Soonest
        };

        return MapPointReader.ReadAsync(
            query,
            async (cursor, size, token) =>
            {
                var page = await _events.SearchAsync(
                    filter with { Cursor = cursor, Limit = size }, token);
                return (page.Items, page.HasMore ? page.NextCursor : null);
            },
            communityEvent => (communityEvent.Venue.Latitude, communityEvent.Venue.Longitude),

            // An event's time on the map is when it starts, not when it was announced: "events in
            // the next seven days" is the question a time range asks of this layer (21 Oct 2026).
            communityEvent => communityEvent.Schedule.StartsAt,
            CommunityLifeLimits.MaxPageSize,
            cancellationToken);
    }
}

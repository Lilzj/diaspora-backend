using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Location;

/// <summary>
/// Contributes post counts to Location's area and country statistics.
/// <para>
/// The counts a neighbourhood hub renders as "12 updates this week" come from here. Counting is
/// a prefix match on the hierarchy path Location itself materialised and B6 copied — no
/// hierarchy reasoning happens in this module, only a string comparison against a value Location
/// computed.
/// </para>
/// </summary>
public sealed class CommunityResourceCounter : ILocationResourceCounter
{
    private readonly ICommunityPostRepository _posts;

    public CommunityResourceCounter(ICommunityPostRepository posts) => _posts = posts;

    public string ResourceType => CommunityNearbyResourceProvider.Key;

    public Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _posts.CountInAreaAsync(path.Value, cancellationToken);
    }

    public Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return _posts.CountInCountryAsync(countryCode.Value, cancellationToken);
    }

    /// <summary>
    /// Where the published, public-audience posts sit inside a viewport, by their own pin
    /// (14 Oct 2026).
    /// <para>
    /// <b>Public audience only</b>, unlike the counts above: a map is shown to anybody who may see
    /// it, and a post written for a neighbourhood or a country must not place itself on that map
    /// for everybody else. A post with no pin is not on the map. A viewport across 180 degrees is
    /// read as its two halves, because the post rectangle filter does not wrap.
    /// </para>
    /// </summary>
    public async Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.CrossesAntimeridian)
        {
            return await ReadAsync(query, query.West, query.East, cancellationToken);
        }

        var east = await ReadAsync(query, query.West, 180d, cancellationToken);
        var west = await ReadAsync(query, -180d, query.East, cancellationToken);

        return new MapPointPage([.. east.Points, .. west.Points], east.IsTruncated || west.IsTruncated);
    }

    private Task<MapPointPage> ReadAsync(
        MapPointQuery query, double west, double east, CancellationToken cancellationToken)
    {
        var filter = new PostQueryFilter
        {
            Statuses = [PostStatus.Published],
            AllowedVisibility = [VisibilityLevel.Public],
            ExcludeExpired = true,
            CountryCode = query.CountryCode,
            BoundingBox = (query.South, west, query.North, east)
        };

        return MapPointReader.ReadAsync(
            query,
            async (cursor, size, token) =>
            {
                var page = await _posts.QueryAsync(
                    filter with { Cursor = FeedCursor.Decode(cursor), Limit = size }, token);
                return (page.Items, page.HasMore ? page.NextCursor : null);
            },
            post => (post.Location.Latitude, post.Location.Longitude),
            CommunityLimits.MaxFeedPageSize,
            cancellationToken);
    }
}

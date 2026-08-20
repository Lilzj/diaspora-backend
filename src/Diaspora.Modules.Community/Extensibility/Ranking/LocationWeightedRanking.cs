using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Extensibility.Feeds;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// Close and urgent first.
/// <para>
/// The ranking the client already implements by hand on its home screen: urgent items within
/// fifty kilometres or in the same city float, and everything else sorts by distance. This moves
/// that rule server-side, where it can be one rule instead of one per screen.
/// </para>
/// <para>
/// <b>Every distance here was computed by Location.</b> This strategy reads a dictionary; it
/// does no geometry. That is the line the brief draws — consume Location, never duplicate
/// geographic behaviour — and it is why the distances arrive in the context rather than being
/// worked out from a pair of coordinates that are sitting right there.
/// </para>
/// <para>
/// An item with no distance sorts last rather than first. A post with no location is not nearby;
/// treating a missing distance as zero would put every unplaced post at the top of a
/// proximity feed, which is the exact inversion of what was asked for.
/// </para>
/// </summary>
public sealed class LocationWeightedRanking : IRankingStrategy
{
    /// <summary>The client's own threshold for what counts as local enough to shout about.</summary>
    private const double UrgentProximityKm = 50d;

    private const double UnknownDistanceKm = 40_075d;

    public string RankingKey => RankingKeys.LocationWeighted;

    public IReadOnlyList<FeedItem> Rank(IReadOnlyList<FeedItem> items, RankingContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);

        return items
            .Select(item => new
            {
                Item = item,
                Distance = ResolveDistance(item, context),
                Urgency = item.Post?.Urgency ?? UrgencyLevel.Normal
            })
            .Select(entry => new
            {
                entry.Item,
                entry.Distance,
                IsUrgentAndNear =
                    entry.Urgency >= UrgencyLevel.Urgent && entry.Distance <= UrgentProximityKm
            })
            .OrderByDescending(entry => entry.IsUrgentAndNear)
            .ThenBy(entry => entry.Distance)
            .ThenByDescending(entry => entry.Item.PublishedAt ?? DateTimeOffset.MinValue)
            .Select(entry => entry.Item with
            {
                // Negated so that a smaller distance is a larger sort key, which is what the
                // cursor's descending contract expects. Without this the second page of a
                // proximity feed comes back in the opposite order from the first.
                SortKey = -entry.Distance,
                DistanceKm = entry.Distance >= UnknownDistanceKm ? null : entry.Distance
            })
            .ToList();
    }

    private static double ResolveDistance(FeedItem item, RankingContext context) =>
        item.DistanceKm
        ?? (context.Distances.TryGetValue(item.Reference.Key, out var distance)
            ? distance
            : UnknownDistanceKm);
}

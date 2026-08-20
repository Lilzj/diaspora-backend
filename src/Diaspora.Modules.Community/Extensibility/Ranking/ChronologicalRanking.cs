using Diaspora.Modules.Community.Extensibility.Feeds;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// Newest first.
/// <para>
/// The floor every other ranking falls back to, and the only one with a property none of the
/// others have: it cannot be gamed. A member cannot make their post newer. That is worth
/// keeping as a first-class, always-available option rather than as a debug mode — for a feed
/// carrying safety information, an unmanipulable ordering is sometimes the correct product
/// answer.
/// </para>
/// </summary>
public sealed class ChronologicalRanking : IRankingStrategy
{
    public string RankingKey => RankingKeys.Chronological;

    public IReadOnlyList<FeedItem> Rank(IReadOnlyList<FeedItem> items, RankingContext context)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select(item => item with
            {
                SortKey = (item.PublishedAt ?? DateTimeOffset.MinValue).ToUnixTimeMilliseconds()
            })
            .OrderByDescending(item => item.SortKey)
            .ThenByDescending(item => item.Reference.Id)
            .ToList();
    }
}

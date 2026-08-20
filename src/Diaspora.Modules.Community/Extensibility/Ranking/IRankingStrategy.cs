using Diaspora.Modules.Community.Extensibility.Feeds;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// Puts feed items in an order.
/// <para>
/// The brief's requirement is that ranking algorithms be replaceable, and this is the whole of
/// what that means in practice: a strategy takes items and a context of pre-resolved signals,
/// and returns items. It cannot query, cannot filter for visibility, and cannot decide what is
/// in the feed — only what order it comes in.
/// </para>
/// <para>
/// <b>Ranking must not drop items.</b> A strategy that returned a subset would silently make a
/// page shorter than the page size, which a client reads as the end of the feed. Suppression is
/// a visibility or moderation decision, made before ranking, by something accountable for it.
/// </para>
/// </summary>
public interface IRankingStrategy
{
    /// <summary>The key a feed or a request names.</summary>
    string RankingKey { get; }

    /// <summary>
    /// Reorders the items. The returned list must contain exactly the items passed in.
    /// <para>
    /// Synchronous by design. A ranking that needed to await something needed a signal that
    /// should have been resolved into the context, and the place to fix that is the context.
    /// </para>
    /// </summary>
    IReadOnlyList<FeedItem> Rank(IReadOnlyList<FeedItem> items, RankingContext context);
}

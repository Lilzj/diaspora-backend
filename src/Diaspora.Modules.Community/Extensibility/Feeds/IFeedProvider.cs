namespace Diaspora.Modules.Community.Extensibility.Feeds;

/// <summary>
/// A feed.
/// <para>
/// The brief asks for feeds that are independently extensible, and this is the shape of that: a
/// feed is a class with a key. Adding one — an AI feed, a saved-search feed, an employer feed —
/// is a new implementation in whichever assembly owns the idea, discovered by the same assembly
/// scan every other seam on this platform uses. Nothing in Community changes, and no
/// <c>switch</c> anywhere grows a case.
/// </para>
/// <para>
/// A provider is responsible for <em>selecting</em> items. It is not responsible for ranking
/// them, for deciding who may see them, or for projecting them — those are a ranking strategy,
/// the visibility evaluators and the mappers respectively. A provider that ranked its own
/// results would make ranking unreplaceable for that feed, which defeats the other half of the
/// brief.
/// </para>
/// </summary>
public interface IFeedProvider
{
    /// <summary>The URL segment this feed answers on. Unique across every module.</summary>
    string FeedKey { get; }

    /// <summary>The ranking applied when the request does not name one.</summary>
    string DefaultRankingKey { get; }

    /// <summary>
    /// Whether this feed can answer at all for the given request. A nearby feed with no
    /// coordinates says no, and the caller gets a clear error rather than an empty page —
    /// silently returning nothing when a required parameter is missing is indistinguishable from
    /// a quiet neighbourhood.
    /// </summary>
    bool CanServe(FeedRequest request) => true;

    /// <summary>Selects the candidates. Ranking, visibility and projection happen elsewhere.</summary>
    Task<FeedPage> GetAsync(FeedRequest request, CancellationToken cancellationToken = default);
}

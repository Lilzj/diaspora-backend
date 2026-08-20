namespace Diaspora.Modules.Marketplace.Extensibility.Views;

/// <summary>
/// Decides whether a view counts as a new one.
/// <para>
/// <b>The brief asks for unique views, and this is the only honest way to get them without a row
/// per view.</b> Storing every view would buy the ability to answer questions nobody asks and cost
/// a table that grows by millions a week and has to be pruned forever. What a de-duplicating
/// tracker costs instead is a cache entry per (listing, viewer) that expires — which is exactly
/// what "unique in a window" means.
/// </para>
/// <para>
/// <b>It is also the brief's caching abstraction.</b> The shipped implementation is an in-process
/// memory cache, which is correct on a single instance and approximately correct on several — a
/// listing viewed once per instance counts as a handful of unique views rather than one, which
/// overstates a number nobody makes a decision on. A deployment that cares registers a
/// distributed implementation and nothing above this interface changes.
/// </para>
/// <para>
/// <b>Never throws and never blocks.</b> A tracker that failed would fail the page render of a
/// listing, and a view count is not worth a listing nobody can open — implementations swallow and
/// answer false, which undercounts rather than breaking.
/// </para>
/// </summary>
public interface IListingViewTracker
{
    /// <summary>
    /// Records a view and says whether it was the viewer's first in the window.
    /// <para>
    /// <paramref name="viewerKey"/> is a member identifier where there is one and a session or
    /// address hash where there is not. It is never persisted: the tracker holds it for as long as
    /// the window lasts and no longer, which is what keeps "unique views" from becoming a log of
    /// who looked at what.
    /// </para>
    /// </summary>
    Task<bool> RegisterViewAsync(
        Guid listingId, string viewerKey, CancellationToken cancellationToken = default);
}

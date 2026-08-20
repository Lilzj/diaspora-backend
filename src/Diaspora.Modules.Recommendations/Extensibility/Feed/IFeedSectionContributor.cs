namespace Diaspora.Modules.Recommendations.Extensibility.Feed;

/// <summary>
/// Implemented by a module to put a row on the home feed.
/// <para>
/// <b>The seam that keeps the feed from becoming a list this module maintains by hand.</b> Without
/// it, every vertical that wanted a home-feed presence would be a change to a switch statement in
/// here — which is the closed <c>PermissionResolver</c> that B2.6 existed to abolish, rebuilt in a
/// different file.
/// </para>
/// <para>
/// Constructed before the container exists: public parameterless constructor, no dependencies.
/// </para>
/// </summary>
public interface IFeedSectionContributor
{
    /// <summary>The contributing module's name. Used to attribute a duplicate-key failure.</summary>
    string Module { get; }

    /// <summary>Every section this module puts on the feed.</summary>
    IEnumerable<FeedSectionDefinition> GetSections();
}

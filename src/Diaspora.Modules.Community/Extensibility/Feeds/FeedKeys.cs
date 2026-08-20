namespace Diaspora.Modules.Community.Extensibility.Feeds;

/// <summary>
/// The feed keys B6 ships.
/// <para>
/// Constants rather than an enum, because the set is <b>open</b>: a module contributes a feed by
/// registering a provider with a new key, and an enum would mean editing this module to add one.
/// These twelve are the ones the frontend contract report found or the brief names, published so
/// that two modules do not independently invent <c>near-me</c> and <c>nearby</c>.
/// </para>
/// <para>
/// Every key is a URL segment, so they are lower-case and hyphenated, and renaming one breaks a
/// bookmarked link.
/// </para>
/// </summary>
public static class FeedKeys
{
    /// <summary>The default: urgent nearby content first, then everything else by relevance.</summary>
    public const string Home = "home";

    /// <summary>Content from members and organizations the viewer follows.</summary>
    public const string Following = "following";

    /// <summary>Content near a coordinate, ordered by distance.</summary>
    public const string Nearby = "nearby";

    /// <summary>Content gaining engagement fastest inside a window.</summary>
    public const string Trending = "trending";

    public const string Country = "country";

    public const string Neighborhood = "neighborhood";

    /// <summary>One member's published content. The client's contributor screen.</summary>
    public const string Contributor = "contributor";

    /// <summary>One organization's published content.</summary>
    public const string Organization = "organization";

    /// <summary>The viewer's saved content.</summary>
    public const string Saved = "saved";

    /// <summary>The viewer's own content, drafts included.</summary>
    public const string Profile = "profile";

    /// <summary>Everything carrying one hashtag.</summary>
    public const string Hashtag = "hashtag";

    /// <summary>
    /// Reserved for B15. Registered by Search when it ships, not by this module — a feed key
    /// with no provider behind it answers 404, which is the honest response to a feed nobody
    /// implements yet.
    /// </summary>
    public const string Search = "search";
}

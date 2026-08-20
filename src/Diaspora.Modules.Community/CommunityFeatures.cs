namespace Diaspora.Modules.Community;

/// <summary>
/// The feature gates the Community module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The client already has the screen for this — <c>FeatureGate</c> renders a lock
/// card naming the feature — and it currently decides locally from the account state. These
/// names are what let it ask the server instead.
/// </para>
/// <para>
/// The gate names mirror the client's <c>featureName</c> strings where the client has one:
/// "Home Feed", "Local Alerts", "Creation Hub" and "Knowledge Hub" all map to a gate here.
/// </para>
/// </summary>
public static class CommunityFeatures
{
    /// <summary>The client's <c>FeatureGate featureName="Home Feed"</c>.</summary>
    public const string Feed = "community.feed";

    /// <summary>The client's <c>FeatureGate featureName="Local Alerts"</c> — posting.</summary>
    public const string Posting = "community.posting";

    /// <summary>The client's <c>FeatureGate featureName="Creation Hub"</c>.</summary>
    public const string Composer = "community.composer";

    public const string Commenting = "community.commenting";

    public const string Reactions = "community.reactions";

    public const string Sharing = "community.sharing";

    public const string Bookmarks = "community.bookmarks";

    public const string Follows = "community.follows";

    public const string Polls = "community.polls";

    public const string Mentions = "community.mentions";

    public const string Hashtags = "community.hashtags";

    /// <summary>Choosing an audience narrower than public.</summary>
    public const string AudienceControl = "community.audience-control";

    /// <summary>The moderator queue.</summary>
    public const string ModerationQueue = "community.moderation-queue";

    // ─── Claimed from the provisional catalogue in PSR-1 ─────────────────────────────────────
    //
    // B2 declared these two before this module existed, and the host has carried them as
    // "Platform (provisional)" ever since. PSR-1 moved them here — same names, same tiers, same
    // behaviour — because a gate about the community feed belongs to the module that owns the
    // community feed, and an entry attributed to nobody is a debt entry that never gets paid.
    //
    // They overlap in meaning with Feed and Posting above, which is the honest state of the
    // platform rather than a mistake introduced here: two names for one idea, both shipped, both
    // held in the frontend's capability map. Retiring either is a versioned client change and is
    // recorded as DEBT-048 rather than taken quietly.

    /// <summary>
    /// Browsing the feed. B2's name for what <see cref="Feed"/> also gates; kept because the
    /// mobile client already switches on it.
    /// </summary>
    public const string Browse = "community.browse";

    /// <summary>
    /// Posting to the feed. B2's name for what <see cref="Posting"/> also gates; kept because the
    /// mobile client already switches on it.
    /// </summary>
    public const string Post = "community.post";
}

namespace Diaspora.Modules.Community.Domain.Policies;

/// <summary>
/// The ceilings the domain enforces.
/// <para>
/// Gathered in one file rather than scattered across the aggregates that use them, because
/// every one of them is a product decision somebody will want to revisit, and a limit nobody
/// can find is a limit that gets duplicated with a different value.
/// </para>
/// </summary>
public static class CommunityLimits
{
    /// <summary>
    /// How deep a reply chain may go.
    /// <para>
    /// The brief asks for unlimited nesting, and the aggregate supports it: a comment stores its
    /// parent, and nothing in the model caps the chain. This is a <em>rendering</em> depth, and
    /// the distinction is the whole point. Past about six levels a mobile thread is a column of
    /// single words, so beyond this depth a reply is stored at its true parent but rendered
    /// flattened into the deepest visible ancestor.
    /// </para>
    /// <para>
    /// Storing the true parent rather than the flattened one is what keeps the decision
    /// reversible. A later client with more horizontal room, or a different flattening rule,
    /// re-renders the same rows correctly. Flattening at write time would destroy the structure
    /// permanently to satisfy today's layout.
    /// </para>
    /// </summary>
    public const int MaxRenderedCommentDepth = 6;

    /// <summary>Hard ceiling on the stored chain. A guard against a cycle, not a product rule.</summary>
    public const int MaxCommentDepth = 100;

    /// <summary>Attachments on one post. Matches the four-slot picker the client renders.</summary>
    public const int MaxPostAttachments = 4;

    /// <summary>Attachments on one comment. One image is the whole of the use case.</summary>
    public const int MaxCommentAttachments = 1;

    /// <summary>Hashtags extracted from one body. Beyond this it is tag spam, not topics.</summary>
    public const int MaxHashtagsPerPost = 10;

    /// <summary>
    /// Mentions resolved in one body. The notification amplifier: without a cap, one post can
    /// notify every member on the platform.
    /// </summary>
    public const int MaxMentionsPerPost = 20;

    public const int MaxMentionsPerComment = 10;

    /// <summary>Options on a poll. Two is a poll; twelve is a survey.</summary>
    public const int MaxPollOptions = 12;

    public const int MinPollOptions = 2;

    /// <summary>Bookmark folders one member may create.</summary>
    public const int MaxBookmarkFolders = 50;

    /// <summary>Revisions kept per post. Older ones are pruned oldest-first.</summary>
    public const int MaxRevisionsRetained = 50;

    /// <summary>How long an author may edit a post before edits are marked publicly.</summary>
    public static readonly TimeSpan SilentEditWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The smallest affected area an alert may declare, in metres of radius. Below this a circle is
    /// indistinguishable from the pin on any map a phone draws.
    /// </summary>
    public const int MinAffectedRadiusMetres = 25;

    /// <summary>
    /// The largest, in metres of radius. Fifty kilometres covers a flood or a regional outage; past
    /// that an alert is about a country, and the country's own channels are the right place for it.
    /// It also bounds how far the nearby query has to look for a circle that reaches the member.
    /// </summary>
    public const int MaxAffectedRadiusMetres = 50_000;

    /// <summary>Feed page size ceiling. Matches the SharedKernel cursor pagination maximum.</summary>
    public const int MaxFeedPageSize = 50;

    public const int DefaultFeedPageSize = 20;

    /// <summary>
    /// How near a reader's active location must be to a neighbourhood-only post's pin, in
    /// kilometres. The neighbourhood audience is this radius around the post's point since
    /// 14 Oct 2026, when the platform retired its place catalogue.
    /// </summary>
    public const double NeighborhoodAudienceRadiusKm = 2d;
}

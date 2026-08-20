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

    /// <summary>
    /// Open reports on one piece of content before it is auto-hidden pending review.
    /// <para>
    /// A blunt instrument, and known to be one: it is also a brigading vector, since a
    /// coordinated group reaching this number can hide anything. It is set high enough to be
    /// expensive to organise, the auto-hide is reversible by one moderator action, and the
    /// alternative — leaving genuinely harmful content up until a human is available — is worse
    /// in the cases that matter most.
    /// </para>
    /// </summary>
    public const int AutoHideReportThreshold = 10;

    /// <summary>How long an author may edit a post before edits are marked publicly.</summary>
    public static readonly TimeSpan SilentEditWindow = TimeSpan.FromMinutes(5);

    /// <summary>Feed page size ceiling. Matches the SharedKernel cursor pagination maximum.</summary>
    public const int MaxFeedPageSize = 50;

    public const int DefaultFeedPageSize = 20;
}

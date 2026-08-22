namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Table names and column widths every Community configuration shares.</summary>
internal static class CommunityPersistenceConstants
{
    public const string PostsTable = "community_posts";
    public const string PostAttachmentsTable = "post_attachments";
    public const string PostHashtagsTable = "post_hashtags";
    public const string PostMentionsTable = "post_mentions";
    public const string PostRevisionsTable = "post_revisions";
    public const string PollsTable = "polls";
    public const string PollOptionsTable = "poll_options";
    public const string PollVotesTable = "poll_votes";
    public const string CommentsTable = "comments";
    public const string CommentMentionsTable = "comment_mentions";
    public const string ReactionsTable = "reactions";
    public const string ReactionSummariesTable = "reaction_summaries";
    public const string ReactionTalliesTable = "reaction_tallies";
    public const string ReactionDefinitionsTable = "reaction_definitions";
    public const string SharesTable = "shares";
    /// <summary>
    /// Prefixed, and not by accident. The B2.6 reference module already owns a table named
    /// <c>bookmarks</c> in its own schema, and SQLite — which the integration suite runs on —
    /// ignores schemas entirely. Two modules whose tables differ only by schema collide there
    /// and nowhere else, which is the worst possible place for a name clash to first appear.
    /// </summary>
    public const string BookmarksTable = "community_bookmarks";
    public const string BookmarkFoldersTable = "community_bookmark_folders";
    public const string FollowsTable = "follows";
    public const string HashtagsTable = "hashtags";
    public const string MentionsTable = "mentions";
    public const string ContentReportsTable = "content_reports";
    /// <summary>Prefixed: Trust owns <c>moderation_decisions</c>. See the bookmarks note.</summary>
    public const string ModerationDecisionsTable = "community_moderation_decisions";
    public const string ContentReviewsTable = "content_reviews";
    public const string VisibilityRulesTable = "visibility_rules";
    public const string VisibilityClausesTable = "visibility_clauses";
    public const string FeedSubscriptionsTable = "feed_subscriptions";

    /// <summary>
    /// Community's own member projection — BG-05a. Prefixed for the reason the bookmarks note
    /// gives: <c>members</c> is a name several modules would plausibly want, and the integration
    /// suite runs on SQLite, which ignores schemas.
    /// </summary>
    public const string MembersTable = "community_members";

    public const int EnumLength = 32;
    public const int SlugLength = 64;
    public const int HandleLength = 128;
    public const int NameLength = 200;
    public const int CountryCodeLength = 2;
    public const int PathLength = 512;

    /// <summary>
    /// A materialised comment path: thirty-two hex characters and a separator per level. Six
    /// hundred and sixty characters holds twenty levels, which is far past anything a client
    /// renders and short enough to index.
    /// </summary>
    public const int CommentPathLength = 1024;

    /// <summary>A content reference in single-column form: a type ordinal and a compact GUID.</summary>
    public const int ContentKeyLength = 48;

    /// <summary>A composite key of a member and a content reference.</summary>
    public const int CompositeKeyLength = 96;
}

namespace Diaspora.Modules.Community;

/// <summary>
/// The permissions the Community module owns.
/// <para>
/// The band expresses one product decision repeatedly: <b>reading is cheap, writing is
/// social, and moderating is staff</b>. Reading a feed opens at <c>Onboarding</c> because a
/// member who cannot see the community has no reason to finish verifying. Posting opens at
/// <c>Verified</c>, because an unverified account posting a fake emergency alert into a
/// neighbourhood feed is the single worst thing this module can be used for. Commenting and
/// reacting sit between the two, at <c>Limited</c>, because they are how a member
/// participates while their verification is in flight.
/// </para>
/// </summary>
public static class CommunityPermissions
{
    /// <summary>Read posts, comments, reactions and feeds the caller is allowed to see.</summary>
    public const string Read = "community:read";

    /// <summary>Create a post. The highest-consequence write in the module.</summary>
    public const string Post = "community:post";

    /// <summary>Edit or delete the caller's own post.</summary>
    public const string ManagePost = "community:post:manage";

    /// <summary>Comment on content, and reply to a comment.</summary>
    public const string Comment = "community:comment";

    /// <summary>Edit or delete the caller's own comment.</summary>
    public const string ManageComment = "community:comment:manage";

    /// <summary>React to content.</summary>
    public const string React = "community:react";

    /// <summary>Share content, internally or outward.</summary>
    public const string Share = "community:share";

    /// <summary>Bookmark content and organise bookmarks into folders.</summary>
    public const string Bookmark = "community:bookmark";

    /// <summary>Follow and unfollow another member or an organization.</summary>
    public const string Follow = "community:follow";

    /// <summary>Create and vote in polls.</summary>
    public const string Poll = "community:poll";

    /// <summary>Report content for review.</summary>
    public const string Report = "community:report";

    /// <summary>Read the moderation queue and every report on it. Staff only.</summary>
    public const string ModerationRead = "community:moderation:read";

    /// <summary>Decide a report: approve, hide, remove, restore. Staff only.</summary>
    public const string Moderate = "community:moderate";

    /// <summary>Pin a post or a comment. Staff, and an author within their own thread.</summary>
    public const string Pin = "community:pin";

    /// <summary>Manage the reaction catalogue and the visibility rule set. Staff only.</summary>
    public const string Administer = "community:administer";
}

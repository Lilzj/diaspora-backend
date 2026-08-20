namespace Diaspora.Modules.Community;

/// <summary>
/// The policy names the module's endpoints reference.
/// <para>
/// One policy per permission, named with dots where the permission uses colons — the
/// convention B2.6 established and B3, B4 and B5 all follow. The kernel validates at startup
/// that every permission named by a policy exists, so a typo here fails the boot rather than
/// silently denying a route forever.
/// </para>
/// </summary>
public static class CommunityPolicies
{
    public const string Read = "community.read";

    public const string Post = "community.post";

    public const string ManagePost = "community.post.manage";

    public const string Comment = "community.comment";

    public const string ManageComment = "community.comment.manage";

    public const string React = "community.react";

    public const string Share = "community.share";

    public const string Bookmark = "community.bookmark";

    public const string Follow = "community.follow";

    public const string Poll = "community.poll";

    public const string Report = "community.report";

    public const string ModerationRead = "community.moderation.read";

    public const string Moderate = "community.moderate";

    public const string Pin = "community.pin";

    public const string Administer = "community.administer";
}

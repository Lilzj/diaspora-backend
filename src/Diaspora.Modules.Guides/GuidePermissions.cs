namespace Diaspora.Modules.Guides;

/// <summary>
/// The permissions the Guides module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use
/// dots — see <see cref="GuidePolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/GuidePermissionContributor</c>, because the module is the only thing
/// that knows what each capability is worth.
/// </para>
/// <para>
/// The split that matters: <b>authoring is Limited, publishing is Verified.</b> Drafting a
/// guide is low-stakes engagement; publishing long-lived knowledge under the platform's name
/// is exactly the progression B2.0.1 gates on proven identity. Staff-only capabilities are
/// role-scoped, not tier-scoped, per the access-tier semantics: a tier measures progression,
/// a role grants privilege.
/// </para>
/// </summary>
public static class GuidePermissions
{
    // Authoring
    public const string Create = "guides:create";
    public const string EditOwn = "guides:edit:own";
    public const string EditAny = "guides:edit:any";
    public const string DeleteOwn = "guides:delete:own";
    public const string ViewDrafts = "guides:drafts:view";

    // Lifecycle
    public const string Publish = "guides:publish";
    public const string Schedule = "guides:schedule";
    public const string ArchiveOwn = "guides:archive:own";
    public const string RestoreOwn = "guides:restore:own";

    // Curation & verification
    public const string Feature = "guides:feature";
    public const string SubmitVerification = "guides:verify:submit";
    public const string Moderate = "guides:moderate";
    public const string ManageCategories = "guides:categories:manage";

    // Engagement
    public const string Vote = "guides:vote";
    public const string Bookmark = "guides:bookmark";
    public const string Report = "guides:report";
    public const string Suggest = "guides:suggest";
    public const string ManageTags = "guides:tags:manage";
    public const string ViewAnalytics = "guides:analytics:view";
}

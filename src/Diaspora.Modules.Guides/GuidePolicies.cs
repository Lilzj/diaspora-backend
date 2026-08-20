namespace Diaspora.Modules.Guides;

/// <summary>
/// The authorisation policies the module's endpoints reference.
/// <para>
/// Dot-separated names where permissions use colons, per the platform convention. Each policy
/// names exactly one permission from <see cref="GuidePermissions"/>; the mapping is declared
/// in <c>Authorization/GuidePolicyContributor</c> and registered with ASP.NET Core by the
/// kernel. Endpoints always reference a policy name, never a raw permission.
/// </para>
/// </summary>
public static class GuidePolicies
{
    public const string Create = "guides.create";
    public const string EditOwn = "guides.edit.own";
    public const string EditAny = "guides.edit.any";
    public const string DeleteOwn = "guides.delete.own";
    public const string ViewDrafts = "guides.drafts.view";

    public const string Publish = "guides.publish";
    public const string Schedule = "guides.schedule";
    public const string ArchiveOwn = "guides.archive.own";
    public const string RestoreOwn = "guides.restore.own";

    public const string Feature = "guides.feature";
    public const string SubmitVerification = "guides.verify.submit";
    public const string Moderate = "guides.moderate";
    public const string ManageCategories = "guides.categories.manage";

    public const string Vote = "guides.vote";
    public const string Bookmark = "guides.bookmark";
    public const string Report = "guides.report";
    public const string Suggest = "guides.suggest";
    public const string ManageTags = "guides.tags.manage";
    public const string ViewAnalytics = "guides.analytics.view";
}

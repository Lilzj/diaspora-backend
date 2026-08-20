namespace Diaspora.Modules.Guides;

/// <summary>
/// The authorisation policies the Knowledge Directory's endpoints reference.
/// <para>
/// Dot-separated where permissions use colons, per the platform convention. Each names exactly one
/// permission from <see cref="KnowledgePermissions"/>; the mapping is declared in
/// <c>Authorization/KnowledgePolicyContributor</c> and registered with ASP.NET Core by the kernel.
/// Endpoints always reference a policy name, never a raw permission.
/// </para>
/// </summary>
public static class KnowledgePolicies
{
    public const string ManageCategories = "guides.knowledge.categories.manage";

    public const string ManageDirectory = "guides.knowledge.directory.manage";

    public const string Curate = "guides.knowledge.curate";

    public const string ManagePaths = "guides.knowledge.paths.manage";

    public const string ViewAnalytics = "guides.knowledge.analytics.view";
}

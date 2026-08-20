namespace Diaspora.Modules.Guides;

/// <summary>
/// The authorisation policies the Country Intelligence Platform's endpoints reference.
/// <para>
/// Dot-separated where permissions use colons, per the platform convention. Each names exactly one
/// permission from <see cref="IntelligencePermissions"/>; the mapping is declared in
/// <c>Authorization/IntelligencePolicyContributor</c> and registered with ASP.NET Core by the kernel.
/// Endpoints always reference a policy name, never a raw permission.
/// </para>
/// </summary>
public static class IntelligencePolicies
{
    public const string ManageNotices = "guides.intelligence.notices.manage";

    public const string VerifyNotices = "guides.intelligence.notices.verify";

    public const string PublishNotices = "guides.intelligence.notices.publish";

    public const string ManageCatalogue = "guides.intelligence.catalogue.manage";

    public const string ManageAuthorities = "guides.intelligence.authorities.manage";

    public const string ManageDirectory = "guides.intelligence.directory.manage";

    public const string ManageVisas = "guides.intelligence.visas.manage";

    public const string ReadAnalytics = "guides.intelligence.analytics.read";
}

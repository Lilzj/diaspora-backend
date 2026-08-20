namespace Diaspora.Modules.Guides;

/// <summary>
/// The permissions the Country Knowledge Directory owns.
/// <para>
/// <b>Prefixed <c>guides:knowledge:</c> rather than <c>knowledge:</c>, and the prefix is a
/// statement about ownership.</b> The Guides module owns this capability band; B8.1 is a phase of
/// B8, not a module beside it. A permission catalogue where <c>knowledge:curate</c> appeared with no
/// module to attribute it to would be the first step towards the second guide-owning module this
/// whole phase exists to prevent.
/// </para>
/// <para>
/// <b>Every one of these is role-scoped, and none of them is tier-scoped.</b> That is the sharpest
/// application of B2.0.1 anywhere on the platform: a tier measures progression and a role grants
/// privilege, and no amount of progression should let a member rearrange the page every newcomer to
/// a country sees. Curation is editorial authority over what the platform tells people, and it is
/// granted, never earned.
/// </para>
/// <para>
/// <b>There is deliberately no read permission.</b> Browsing a country's knowledge requires nothing
/// at all — a knowledge base that demands an account before it will answer "how do I register a
/// residence permit" has no way of persuading anybody to make one.
/// </para>
/// </summary>
public static class KnowledgePermissions
{
    /// <summary>Create, rename, archive and restore navigation rows.</summary>
    public const string ManageCategories = "guides:knowledge:categories:manage";

    /// <summary>Open, arrange, publish and archive a country's directory.</summary>
    public const string ManageDirectory = "guides:knowledge:directory:manage";

    /// <summary>Feature guides on a country shelf and set what to read next.</summary>
    public const string Curate = "guides:knowledge:curate";

    /// <summary>Write and publish curated onboarding routes.</summary>
    public const string ManagePaths = "guides:knowledge:paths:manage";

    /// <summary>Read the usage figures, the category breakdown and the unanswered searches.</summary>
    public const string ViewAnalytics = "guides:knowledge:analytics:view";
}

namespace Diaspora.Modules.Guides.Domain.Policies;

/// <summary>
/// The ceilings the Knowledge Directory aggregates enforce.
/// <para>
/// Constants rather than options, for the same reason <see cref="GuideLimits"/> gives: a bound
/// that can be raised per deployment is a bound the domain cannot rely on, and every invariant
/// that depends on it becomes conditional.
/// </para>
/// <para>
/// The numbers here are smaller than B8's on purpose, and the reason is the difference between
/// content and curation. A guide may have a hundred sections because somebody wrote them; a
/// country directory may have sixty categories because somebody arranged them by hand, and a
/// directory nobody could arrange by hand is a directory nobody will. Every one of these is the
/// point past which the editorial surface stops being usable, not the point past which the
/// database struggles.
/// </para>
/// </summary>
public static class KnowledgeLimits
{
    /// <summary>Characters in a knowledge category key. Matches the guide category key width.</summary>
    public const int MaxCategoryKey = 64;

    /// <summary>Characters in a knowledge category name.</summary>
    public const int MaxCategoryName = 120;

    /// <summary>Characters in a knowledge category description.</summary>
    public const int MaxCategoryDescription = 1_000;

    /// <summary>Characters in an icon name. Opaque to the server; the client resolves it.</summary>
    public const int MaxIcon = 100;

    /// <summary>
    /// Guide categories one knowledge category may draw from.
    /// <para>
    /// More than one, because the directory curates: "Getting Started" legitimately draws from
    /// <c>getting-started</c> and <c>orientation</c>. Not many more, because a navigation node
    /// sourcing from a dozen taxonomies has stopped being navigation.
    /// </para>
    /// </summary>
    public const int MaxCategorySources = 10;

    /// <summary>Provider service categories one knowledge category may recommend.</summary>
    public const int MaxProviderCategories = 10;

    /// <summary>Localizations one category or directory may carry.</summary>
    public const int MaxLocalizations = 40;

    /// <summary>Characters in a BCP-47 locale tag. <c>fr</c>, <c>fr-MU</c>, <c>pt-BR</c>.</summary>
    public const int MaxLocale = 16;

    /// <summary>Categories one country directory may list.</summary>
    public const int MaxDirectoryEntries = 60;

    /// <summary>Guides one country directory may feature, across every category.</summary>
    public const int MaxFeaturedGuides = 50;

    /// <summary>Characters in the display name of a country directory.</summary>
    public const int MaxDirectoryName = 150;

    /// <summary>Characters in the summary paragraph of a country directory.</summary>
    public const int MaxDirectorySummary = 2_000;

    /// <summary>Languages a country directory may declare as spoken.</summary>
    public const int MaxDirectoryLanguages = 20;

    /// <summary>Characters in a curator note attached to a featured guide or a path step.</summary>
    public const int MaxCuratorNote = 500;

    /// <summary>Characters in a learning path key.</summary>
    public const int MaxPathKey = 100;

    /// <summary>Characters in a learning path title.</summary>
    public const int MaxPathTitle = 200;

    /// <summary>Characters in a learning path description.</summary>
    public const int MaxPathDescription = 2_000;

    /// <summary>Steps in one learning path. Seven is a good onboarding; thirty is a syllabus.</summary>
    public const int MaxPathSteps = 30;

    /// <summary>Characters in a step title.</summary>
    public const int MaxStepTitle = 200;

    /// <summary>Characters in a step description.</summary>
    public const int MaxStepDescription = 1_000;

    /// <summary>Guides one recommendation set may point at.</summary>
    public const int MaxRecommendedReads = 12;

    /// <summary>Characters in a normalized search phrase kept for analytics.</summary>
    public const int MaxSearchTerm = 200;

    /// <summary>Trending topics one country dashboard returns.</summary>
    public const int MaxTrendingTopics = 20;

    /// <summary>Rows in one page of a knowledge listing.</summary>
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 20;

    /// <summary>Guides one dashboard shelf carries. A dashboard is a summary, not a browse.</summary>
    public const int DashboardShelfSize = 6;

    /// <summary>Categories one dashboard breakdown returns before it is a directory instead.</summary>
    public const int DashboardCategoryLimit = 30;
}

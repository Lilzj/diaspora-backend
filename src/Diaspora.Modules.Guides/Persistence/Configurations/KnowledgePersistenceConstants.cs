namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Knowledge Directory configuration shares.
/// <para>
/// <b>Every table name is <c>knowledge_</c>-prefixed, and the prefix is not decoration.</b> SQLite
/// — which the integration suite runs on — ignores schemas entirely, so two modules whose tables
/// differ only by schema collide there and nowhere else, which is the worst possible place for a
/// name clash to first appear. <c>categories</c>, <c>learning_paths</c> and
/// <c>search_terms</c> are all names some later module will want. B6, B7 and B8 each record the
/// same trap.
/// </para>
/// <para>
/// These sit in the <c>guides</c> schema alongside B8's own tables, because B8.1 is the same module:
/// one schema, one database contributor, one migration. A second schema would suggest a second
/// owner, and the whole architectural claim of this phase is that curation and content have one.
/// </para>
/// </summary>
internal static class KnowledgePersistenceConstants
{
    public const string CategoriesTable = "knowledge_categories";
    public const string CategoryLocalizationsTable = "knowledge_category_localizations";
    public const string CategorySourcesTable = "knowledge_category_sources";
    public const string CategoryProviderCategoriesTable = "knowledge_category_provider_categories";

    public const string CountryDirectoriesTable = "knowledge_country_directories";
    public const string CountryDirectoryEntriesTable = "knowledge_country_directory_entries";
    public const string CountryFeaturedGuidesTable = "knowledge_country_featured_guides";
    public const string CountryDirectoryLocalizationsTable = "knowledge_country_directory_localizations";

    public const string LearningPathsTable = "knowledge_learning_paths";
    public const string LearningPathStepsTable = "knowledge_learning_path_steps";

    public const string RecommendationsTable = "knowledge_recommendations";
    public const string RecommendedReadsTable = "knowledge_recommended_reads";

    public const string CountryAnalyticsTable = "knowledge_country_analytics";
    public const string CategoryViewsTable = "knowledge_category_views";
    public const string SearchTermsTable = "knowledge_search_terms";

    public const string CountryContextSummariesTable = "knowledge_country_context_summaries";
    public const string CountryContextCountersTable = "knowledge_country_context_counters";

    /// <summary>Enum names stored as text. Matches B8's width so the two never disagree.</summary>
    public const int EnumLength = 32;

    /// <summary>ISO 3166-1 alpha-2, or the empty string on a search term with no country.</summary>
    public const int CountryCodeLength = 2;

    /// <summary>A counter kind — <c>jobs</c>, <c>housing</c>. Open-ended, so generously sized.</summary>
    public const int CounterKindLength = 64;

    /// <summary>A relative API route on a contributing module.</summary>
    public const int RouteLength = 200;
}

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Table names and column widths, in one place.
/// <para>
/// Declared once rather than inline for the reason every module since B5 records: a name repeated in
/// two configurations is a name that will differ in one of them, and the difference shows up as a
/// migration that drops a table.
/// </para>
/// </summary>
internal static class RecommendationPersistenceConstants
{
    public const string SignalsTable = "user_signals";
    public const string ProfilesTable = "personalization_profiles";
    public const string ProfileInterestsTable = "profile_interests";
    public const string ProfileLocationsTable = "profile_location_affinities";
    public const string ProfileActivityTable = "profile_activity_summaries";
    public const string PreferencesTable = "personalization_preferences";
    public const string PreferenceCategoriesTable = "preference_categories";
    public const string PreferenceBlocksTable = "preference_blocked_targets";
    public const string PreferenceSurfacesTable = "preference_surfaces";
    public const string PreferenceLocationsTable = "preference_favorite_locations";
    public const string CandidatesTable = "recommendation_candidates";
    public const string CandidateAttributesTable = "recommendation_candidate_attributes";
    public const string SetsTable = "recommendation_sets";
    public const string SetItemsTable = "recommendation_items";
    public const string ItemContributionsTable = "recommendation_item_contributions";
    public const string FeedbackTable = "recommendation_feedback";
    public const string PoliciesTable = "recommendation_policies";
    public const string PolicyParametersTable = "recommendation_policy_parameters";
    public const string StrategiesTable = "ranking_strategies";
    public const string StrategyParametersTable = "ranking_strategy_parameters";
    public const string AnalyticsTable = "recommendation_analytics";
    public const string AnalyticsBreakdownsTable = "recommendation_analytics_breakdowns";

    /// <summary>Enum names are stored as text; nothing in this module has a longer one.</summary>
    public const int EnumLength = 32;

    public const int KeyLength = 128;
    public const int LanguageLength = 16;
    public const int CountryLength = 8;
    public const int PlaceLength = 128;
    public const int TitleLength = 256;
    public const int SummaryLength = 1024;
    public const int SlugLength = 256;
    public const int DescriptionLength = 512;
    public const int ReasonLength = 512;
    public const int ResourceKeyLength = 128;
    public const int MediaReferenceLength = 256;
    public const int ModuleLength = 64;
    public const int EventNameLength = 256;

    /// <summary>
    /// The signal-kind list on an explanation, and the strategy-key list on a set.
    /// <para>
    /// Generous because both are joined lists whose length grows with the number of registered
    /// strategies, and a truncation here would silently shorten an explanation.
    /// </para>
    /// </summary>
    public const int JoinedListLength = 512;

    /// <summary>Money-grade precision, because a score of 0.5 and 0.500001 must order stably.</summary>
    public const int ScorePrecision = 18;

    public const int ScoreScale = 6;
}

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>Table names and column widths every Providers configuration shares.</summary>
internal static class ProviderPersistenceConstants
{
    public const string ProvidersTable = "providers";
    public const string ProviderMembersTable = "provider_members";
    public const string ProviderServicesTable = "provider_services";
    public const string ServiceOfferingsTable = "service_offerings";
    public const string ProviderServiceAreasTable = "provider_service_areas";
    public const string ProviderLocationsTable = "provider_locations";
    public const string ProviderLanguagesTable = "provider_languages";
    public const string ProviderContactMethodsTable = "provider_contact_methods";
    public const string ProviderGalleryTable = "provider_gallery";
    public const string ProviderCertificationsTable = "provider_certifications";
    public const string ProviderTagsTable = "provider_tags";
    public const string ProviderOperatingHoursTable = "provider_operating_hours";
    public const string ProviderAvailabilityWindowsTable = "provider_availability_windows";
    public const string ProviderAvailabilityExceptionsTable = "provider_availability_exceptions";
    public const string ProviderVerificationsTable = "provider_verifications";

    /// <summary>
    /// Prefixed, and not by accident. Trust already owns a table conceptually named
    /// <c>categories</c>-adjacent, and SQLite — which the integration suite runs on — ignores
    /// schemas entirely. Two modules whose tables differ only by schema collide there and
    /// nowhere else, which is the worst possible place for a name clash to first appear. B6
    /// recorded the same trap over <c>bookmarks</c> and <c>moderation_decisions</c>.
    /// </summary>
    public const string ServiceCategoriesTable = "provider_service_categories";
    public const string ServiceCategoryAliasesTable = "provider_category_aliases";
    public const string ServiceCategoryTranslationsTable = "provider_category_translations";
    public const string ServiceCategoryKeywordsTable = "provider_category_keywords";

    /// <summary>Prefixed: several modules will eventually want a table called <c>reviews</c>.</summary>
    public const string ProviderReviewsTable = "provider_reviews";

    public const string ProviderRecommendationsTable = "provider_recommendations";

    public const int EnumLength = 32;
    public const int SlugLength = 120;
    public const int CategorySlugLength = 80;
    public const int NameLength = 200;
    public const int LongNameLength = 300;
    public const int CountryCodeLength = 2;
    public const int CurrencyLength = 3;
    public const int LanguageCodeLength = 12;
    public const int TimeZoneLength = 64;

    /// <summary>Location's materialised path, copied. Matches B4's own column width.</summary>
    public const int HierarchyPathLength = 512;

    /// <summary>The category tree's own materialised path.</summary>
    public const int CategoryPathLength = 512;

    /// <summary>
    /// Money. Eighteen digits with four after the point holds every currency the platform will
    /// meet, including the ones where a coffee costs six figures.
    /// </summary>
    public const int MoneyPrecision = 18;

    public const int MoneyScale = 4;
}

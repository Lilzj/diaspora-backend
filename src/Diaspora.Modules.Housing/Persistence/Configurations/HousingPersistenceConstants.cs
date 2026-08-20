namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Housing configuration shares.
/// <para>
/// <b>Every table name is prefixed, and not by accident.</b> SQLite - which the integration suite
/// runs on - ignores schemas entirely, so two modules whose tables differ only by schema collide
/// there and nowhere else, which is the worst possible place for a name clash to first appear.
/// <c>listings</c>, <c>reservations</c>, <c>reports</c>, <c>amenities</c> and <c>analytics</c> are
/// all names another module will eventually want; the prefix is what stops the collision ever
/// happening. B6, B7, B8, B9 and B10 each record the same trap.
/// </para>
/// <para>
/// <c>housing_neighborhoods</c> is the one to read twice. It holds neighbourhood <em>profiles</em>,
/// not neighbourhoods: the places themselves are B4's <c>location_areas</c>, and this table's rows
/// each point at one. A future reader who assumes the name means Housing owns neighbourhoods will
/// draw exactly the wrong conclusion about the module.
/// </para>
/// </summary>
internal static class HousingPersistenceConstants
{
    // ── Listing aggregate ───────────────────────────────────────────────────────────────────

    public const string ListingsTable = "housing_listings";
    public const string ListingAmenitiesTable = "housing_listing_amenities";
    public const string ListingHouseRulesTable = "housing_listing_house_rules";
    public const string ListingMediaTable = "housing_listing_media";
    public const string ListingProviderReferencesTable = "housing_listing_providers";
    public const string ListingGuideReferencesTable = "housing_listing_guides";
    public const string ListingModerationNotesTable = "housing_listing_moderation_notes";

    // ── Landlord aggregate ──────────────────────────────────────────────────────────────────

    public const string LandlordsTable = "housing_landlords";

    // ── Relocation workflow ─────────────────────────────────────────────────────────────────

    public const string ViewingsTable = "housing_viewings";
    public const string ViewingHistoryTable = "housing_viewing_history";
    public const string ReservationsTable = "housing_reservations";
    public const string ReservationHistoryTable = "housing_reservation_history";

    // ── Neighbourhood profiles. See the class summary: profiles, not neighbourhoods. ────────

    public const string NeighborhoodProfilesTable = "housing_neighborhoods";
    public const string NeighborhoodLanguagesTable = "housing_neighborhood_languages";
    public const string InsightsTable = "housing_neighborhood_insights";
    public const string InsightEndorsementsTable = "housing_neighborhood_insight_endorsements";

    // ── Catalogues ──────────────────────────────────────────────────────────────────────────

    public const string AccommodationTypesTable = "housing_accommodation_types";
    public const string AmenitiesTable = "housing_amenities";

    // ── Safety ──────────────────────────────────────────────────────────────────────────────

    public const string ReportsTable = "housing_reports";

    // ── Measurement ─────────────────────────────────────────────────────────────────────────

    public const string AnalyticsTable = "housing_listing_analytics";
    public const string AnalyticsDailyTable = "housing_listing_analytics_daily";

    // ── Column widths ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every enum is stored as its name, not its number. Readable in a query window, and safe
    /// against somebody reordering the enum.
    /// </summary>
    public const int EnumLength = 32;

    public const int CountryCodeLength = 2;

    public const int CurrencyCodeLength = 3;

    /// <summary>
    /// Money. 18 digits with 2 decimals: enough for a deposit on a property in any currency the
    /// platform will see, including ones with very small units.
    /// </summary>
    public const string MoneyColumnType = "decimal(18,2)";

    /// <summary>
    /// Bathrooms. One decimal place, because a half-bathroom is a normal thing to advertise and
    /// rounding it either way misdescribes the property.
    /// </summary>
    public const string BathroomColumnType = "decimal(4,1)";

    /// <summary>
    /// A coordinate. Nine total digits with six after the point is roughly a tenth of a metre,
    /// which is far finer than anything this module publishes and exactly what it stores.
    /// </summary>
    public const string CoordinateColumnType = "decimal(9,6)";
}

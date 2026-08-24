namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Marketplace configuration shares.
/// <para>
/// <b>Every table name is prefixed, and not by accident.</b> SQLite — which the integration suite
/// runs on — ignores schemas entirely, so two modules whose tables differ only by schema collide
/// there and nowhere else, which is the worst possible place for a name clash to first appear.
/// <c>listings</c>, <c>categories</c>, <c>offers</c>, <c>reservations</c>, <c>reports</c> and
/// <c>analytics</c> are all names another module will eventually want; the prefix is what stops
/// the collision ever happening. B6, B7, B8 and B9 each record the same trap.
/// </para>
/// </summary>
internal static class MarketplacePersistenceConstants
{
    // ── Listing aggregate ───────────────────────────────────────────────────────────────────

    public const string ListingsTable = "marketplace_listings";
    public const string ListingAttributesTable = "marketplace_listing_attributes";
    public const string ListingTagsTable = "marketplace_listing_tags";
    public const string ListingMediaTable = "marketplace_listing_media";
    public const string ListingProviderReferencesTable = "marketplace_listing_providers";
    public const string ListingGuideReferencesTable = "marketplace_listing_guides";
    public const string ListingModerationNotesTable = "marketplace_listing_moderation_notes";

    // ── Seller aggregate ────────────────────────────────────────────────────────────────────

    public const string SellersTable = "marketplace_sellers";

    // ── Commerce aggregates ─────────────────────────────────────────────────────────────────

    public const string OffersTable = "marketplace_offers";
    public const string OfferHistoryTable = "marketplace_offer_history";
    public const string ReservationsTable = "marketplace_reservations";
    public const string SellerReviewsTable = "marketplace_seller_reviews";
    public const string ReservationHistoryTable = "marketplace_reservation_history";

    // ── Catalogues ──────────────────────────────────────────────────────────────────────────

    public const string CategoriesTable = "marketplace_categories";
    public const string AttributeDefinitionsTable = "marketplace_attribute_definitions";
    public const string AttributeChoicesTable = "marketplace_attribute_choices";

    // ── Member-owned rows ───────────────────────────────────────────────────────────────────

    public const string SavedListingsTable = "marketplace_saved_listings";
    public const string ReportsTable = "marketplace_listing_reports";
    public const string PromotionsTable = "marketplace_listing_promotions";
    public const string ConversationsTable = "marketplace_conversations";

    // ── Measurement ─────────────────────────────────────────────────────────────────────────

    public const string AnalyticsTable = "marketplace_listing_analytics";
    public const string AnalyticsDailyTable = "marketplace_listing_analytics_daily";

    // ── Column widths ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every enum is stored as its name, not its number. Readable in a query window, and safe
    /// against somebody reordering the enum.
    /// </summary>
    public const int EnumLength = 32;

    public const int CountryCodeLength = 2;

    public const int CurrencyCodeLength = 3;

    /// <summary>
    /// Money. 18 digits with 2 decimals: enough for a vehicle or a property in any currency the
    /// platform will see, including ones with very small units.
    /// </summary>
    public const string MoneyColumnType = "decimal(18,2)";

    /// <summary>
    /// A parsed numeric attribute value. Wider on the decimal side than money, because "0.75" litres
    /// and "1.8" metres are ordinary attribute values and rounding them to pennies would be wrong.
    /// </summary>
    public const string AttributeNumberColumnType = "decimal(20,6)";
}

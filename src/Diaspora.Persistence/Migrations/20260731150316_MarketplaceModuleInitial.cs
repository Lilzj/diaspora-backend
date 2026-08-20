using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketplace");

            migrationBuilder.CreateTable(
                name: "marketplace_attribute_definitions",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ValueKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsFilterable = table.Column<bool>(type: "boolean", nullable: false),
                    Unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_attribute_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_categories",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParentKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    IconKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ListingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_conversations",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalConversationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OpeningNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_analytics",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TotalViews = table.Column<long>(type: "bigint", nullable: false),
                    TotalUniqueViews = table.Column<long>(type: "bigint", nullable: false),
                    TotalSaves = table.Column<long>(type: "bigint", nullable: false),
                    TotalShares = table.Column<long>(type: "bigint", nullable: false),
                    TotalOffers = table.Column<long>(type: "bigint", nullable: false),
                    TotalReservations = table.Column<long>(type: "bigint", nullable: false),
                    TotalSales = table.Column<long>(type: "bigint", nullable: false),
                    TotalImpressions = table.Column<long>(type: "bigint", nullable: false),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstOfferAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstSaleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeToFirstSale = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_promotions",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_reports",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listings",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ListingType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Condition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price_disclosed = table.Column<bool>(type: "boolean", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    price_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    price_minimum_offer = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    stock_reserved = table.Column<int>(type: "integer", nullable: false),
                    stock_sold = table.Column<int>(type: "integer", nullable: false),
                    stock_tracked = table.Column<bool>(type: "boolean", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighbourhood = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    offers_pickup = table.Column<bool>(type: "boolean", nullable: false),
                    offers_delivery = table.Column<bool>(type: "boolean", nullable: false),
                    offers_shipping = table.Column<bool>(type: "boolean", nullable: false),
                    is_digital = table.Column<bool>(type: "boolean", nullable: false),
                    delivery_radius_km = table.Column<int>(type: "integer", nullable: true),
                    fulfillment_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sku = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    product_year = table.Column<int>(type: "integer", nullable: true),
                    ContactPreference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    FeaturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SoldAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    unique_view_count = table.Column<long>(type: "bigint", nullable: false),
                    save_count = table.Column<long>(type: "bigint", nullable: false),
                    share_count = table.Column<long>(type: "bigint", nullable: false),
                    offer_count = table.Column<long>(type: "bigint", nullable: false),
                    reservation_count = table.Column<long>(type: "bigint", nullable: false),
                    search_impressions = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_offers",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AwaitingParty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HasSellerResponded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_reservations",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    collect_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collect_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collection_place = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgreedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_saved_listings",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NotifyOnPriceChange = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_saved_listings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_sellers",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    seller_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_website = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_preference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PrimaryCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    BannerMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stat_published_listings = table.Column<int>(type: "integer", nullable: false),
                    stat_sold_listings = table.Column<int>(type: "integer", nullable: false),
                    stat_completed_reservations = table.Column<int>(type: "integer", nullable: false),
                    stat_cancelled_reservations = table.Column<int>(type: "integer", nullable: false),
                    stat_accepted_offers = table.Column<int>(type: "integer", nullable: false),
                    stat_response_minutes = table.Column<long>(type: "bigint", nullable: false),
                    stat_responded_conversations = table.Column<int>(type: "integer", nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_sellers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_attribute_choices",
                schema: "marketplace",
                columns: table => new
                {
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_attribute_choices", x => new { x.definition_id, x.Value });
                    table.ForeignKey(
                        name: "FK_marketplace_attribute_choices_marketplace_attribute_definit~",
                        column: x => x.definition_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_attribute_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_analytics_daily",
                schema: "marketplace",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    analytics_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    UniqueViews = table.Column<int>(type: "integer", nullable: false),
                    Saves = table.Column<int>(type: "integer", nullable: false),
                    Shares = table.Column<int>(type: "integer", nullable: false),
                    Offers = table.Column<int>(type: "integer", nullable: false),
                    Reservations = table.Column<int>(type: "integer", nullable: false),
                    Sales = table.Column<int>(type: "integer", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_analytics_daily", x => new { x.analytics_id, x.Day });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_analytics_daily_marketplace_listing_ana~",
                        column: x => x.analytics_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listing_analytics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_attributes",
                schema: "marketplace",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_attributes", x => new { x.listing_id, x.Key });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_attributes_marketplace_listings_listing~",
                        column: x => x.listing_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_guides",
                schema: "marketplace",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_guides", x => new { x.listing_id, x.GuideId });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_guides_marketplace_listings_listing_id",
                        column: x => x.listing_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_media",
                schema: "marketplace",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_media", x => new { x.listing_id, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_media_marketplace_listings_listing_id",
                        column: x => x.listing_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_moderation_notes",
                schema: "marketplace",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MarketplaceListingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_moderation_notes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_marketplace_listing_moderation_notes_marketplace_listings_M~",
                        column: x => x.MarketplaceListingId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_providers",
                schema: "marketplace",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_providers", x => new { x.listing_id, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_providers_marketplace_listings_listing_~",
                        column: x => x.listing_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listing_tags",
                schema: "marketplace",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listing_tags", x => new { x.listing_id, x.Slug });
                    table.ForeignKey(
                        name: "FK_marketplace_listing_tags_marketplace_listings_listing_id",
                        column: x => x.listing_id,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_offer_history",
                schema: "marketplace",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Party = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ListingOfferId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_offer_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_marketplace_offer_history_marketplace_offers_ListingOfferId",
                        column: x => x.ListingOfferId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_reservation_history",
                schema: "marketplace",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ListingReservationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_reservation_history", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_marketplace_reservation_history_marketplace_reservations_Li~",
                        column: x => x.ListingReservationId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_attribute_definitions_path",
                schema: "marketplace",
                table: "marketplace_attribute_definitions",
                column: "CategoryPath");

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_attribute_definitions_key_path",
                schema: "marketplace",
                table: "marketplace_attribute_definitions",
                columns: new[] { "Key", "CategoryPath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_categories_parent_order",
                schema: "marketplace",
                table: "marketplace_categories",
                columns: new[] { "ParentKey", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_categories_path",
                schema: "marketplace",
                table: "marketplace_categories",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_categories_key",
                schema: "marketplace",
                table: "marketplace_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_conversations_buyer",
                schema: "marketplace",
                table: "marketplace_conversations",
                columns: new[] { "BuyerUserId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_conversations_seller",
                schema: "marketplace",
                table: "marketplace_conversations",
                columns: new[] { "SellerUserId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_conversations_listing_buyer",
                schema: "marketplace",
                table: "marketplace_conversations",
                columns: new[] { "ListingId", "BuyerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_analytics_seller",
                schema: "marketplace",
                table: "marketplace_listing_analytics",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_analytics_trends",
                schema: "marketplace",
                table: "marketplace_listing_analytics",
                columns: new[] { "CategoryKey", "CountryCode" });

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_analytics_listing",
                schema: "marketplace",
                table: "marketplace_listing_analytics",
                column: "ListingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_analytics_daily_day",
                schema: "marketplace",
                table: "marketplace_listing_analytics_daily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listing_attributes_key_number",
                schema: "marketplace",
                table: "marketplace_listing_attributes",
                columns: new[] { "Key", "NumericValue" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listing_attributes_key_value",
                schema: "marketplace",
                table: "marketplace_listing_attributes",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_listing_moderation_notes_MarketplaceListingId",
                schema: "marketplace",
                table: "marketplace_listing_moderation_notes",
                column: "MarketplaceListingId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_promotions_expiry",
                schema: "marketplace",
                table: "marketplace_listing_promotions",
                columns: new[] { "EndsAt", "IsCancelled" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_promotions_listing_window",
                schema: "marketplace",
                table: "marketplace_listing_promotions",
                columns: new[] { "ListingId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reports_queue",
                schema: "marketplace",
                table: "marketplace_listing_reports",
                columns: new[] { "Status", "Reason", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reports_seller",
                schema: "marketplace",
                table: "marketplace_listing_reports",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_reports_listing_reporter",
                schema: "marketplace",
                table: "marketplace_listing_reports",
                columns: new[] { "ListingId", "ReporterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listing_tags_slug",
                schema: "marketplace",
                table: "marketplace_listing_tags",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_area_path",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "area_path");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_brand",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "brand");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_category",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_city",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "city_name");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_country",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_delivery",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "offers_delivery");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_expires_at",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_featured",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_moderation",
                schema: "marketplace",
                table: "marketplace_listings",
                columns: new[] { "ModerationState", "ReportCount" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_price",
                schema: "marketplace",
                table: "marketplace_listings",
                columns: new[] { "price_currency", "price_amount" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_published_at",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_seller_status",
                schema: "marketplace",
                table: "marketplace_listings",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_shipping",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "offers_shipping");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_status_published",
                schema: "marketplace",
                table: "marketplace_listings",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_title",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_verification_id",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_view_count",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "view_count");

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_listings_slug",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_offer_history_ListingOfferId",
                schema: "marketplace",
                table: "marketplace_offer_history",
                column: "ListingOfferId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_offers_buyer_status",
                schema: "marketplace",
                table: "marketplace_offers",
                columns: new[] { "BuyerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_offers_expires_at",
                schema: "marketplace",
                table: "marketplace_offers",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_offers_listing_buyer",
                schema: "marketplace",
                table: "marketplace_offers",
                columns: new[] { "ListingId", "BuyerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_offers_seller_status",
                schema: "marketplace",
                table: "marketplace_offers",
                columns: new[] { "SellerId", "Status", "AwaitingParty" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_reservation_history_ListingReservationId",
                schema: "marketplace",
                table: "marketplace_reservation_history",
                column: "ListingReservationId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reservations_buyer_status",
                schema: "marketplace",
                table: "marketplace_reservations",
                columns: new[] { "BuyerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reservations_collect_until",
                schema: "marketplace",
                table: "marketplace_reservations",
                column: "collect_until");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reservations_listing_buyer",
                schema: "marketplace",
                table: "marketplace_reservations",
                columns: new[] { "ListingId", "BuyerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_reservations_seller_status",
                schema: "marketplace",
                table: "marketplace_reservations",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_saved_listing_watchers",
                schema: "marketplace",
                table: "marketplace_saved_listings",
                columns: new[] { "ListingId", "NotifyOnPriceChange" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_saved_user_created",
                schema: "marketplace",
                table: "marketplace_saved_listings",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_saved_user_listing",
                schema: "marketplace",
                table: "marketplace_saved_listings",
                columns: new[] { "UserId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_sellers_country_status",
                schema: "marketplace",
                table: "marketplace_sellers",
                columns: new[] { "PrimaryCountryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_sellers_display_name",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_sellers_provider",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_sellers_verification_id",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_sellers_verification_state",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "verification_state");

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_sellers_owner",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_marketplace_sellers_slug",
                schema: "marketplace",
                table: "marketplace_sellers",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_attribute_choices",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_categories",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_conversations",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_analytics_daily",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_attributes",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_guides",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_media",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_moderation_notes",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_promotions",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_providers",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_reports",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_tags",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_offer_history",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_reservation_history",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_saved_listings",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_sellers",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_attribute_definitions",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listing_analytics",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_listings",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_offers",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "marketplace_reservations",
                schema: "marketplace");
        }
    }
}

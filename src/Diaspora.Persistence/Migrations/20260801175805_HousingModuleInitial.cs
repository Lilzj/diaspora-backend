using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HousingModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "housing");

            migrationBuilder.CreateTable(
                name: "housing_accommodation_types",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IconKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemporary = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_accommodation_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_amenities",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Group = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IconKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsAccessibilityFeature = table.Column<bool>(type: "boolean", nullable: false),
                    IsFilterable = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_amenities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_landlords",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    landlord_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PrimaryCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_website = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    contact_public = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Bio = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    LogoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verification_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    active_listings = table.Column<int>(type: "integer", nullable: false),
                    total_listings = table.Column<int>(type: "integer", nullable: false),
                    completed_lets = table.Column<int>(type: "integer", nullable: false),
                    viewing_requests = table.Column<int>(type: "integer", nullable: false),
                    viewings_answered = table.Column<int>(type: "integer", nullable: false),
                    no_show_count = table.Column<int>(type: "integer", nullable: false),
                    last_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_housing_landlords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_analytics",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    NeighborhoodAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AccommodationTypeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TotalViews = table.Column<long>(type: "bigint", nullable: false),
                    UniqueViews = table.Column<long>(type: "bigint", nullable: false),
                    ViewingRequests = table.Column<long>(type: "bigint", nullable: false),
                    ViewingsCompleted = table.Column<long>(type: "bigint", nullable: false),
                    Reservations = table.Column<long>(type: "bigint", nullable: false),
                    ReservationsAbandoned = table.Column<long>(type: "bigint", nullable: false),
                    Occupations = table.Column<long>(type: "bigint", nullable: false),
                    Saves = table.Column<long>(type: "bigint", nullable: false),
                    Reports = table.Column<long>(type: "bigint", nullable: false),
                    TimeToLet = table.Column<TimeSpan>(type: "interval", nullable: true),
                    LetAtAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LetAtCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    LetAtPeriod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstOccupiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_listings",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccommodationTypeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    price_disclosed = table.Column<bool>(type: "boolean", nullable: false),
                    rent_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    rent_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    rent_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    utilities_included = table.Column<bool>(type: "boolean", nullable: false),
                    internet_included = table.Column<bool>(type: "boolean", nullable: false),
                    bedrooms = table.Column<int>(type: "integer", nullable: false),
                    bathrooms = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    max_occupants = table.Column<int>(type: "integer", nullable: true),
                    floor_area_sqm = table.Column<int>(type: "integer", nullable: true),
                    available_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    available_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_units = table.Column<int>(type: "integer", nullable: false),
                    reserved_units = table.Column<int>(type: "integer", nullable: false),
                    occupied_units = table.Column<int>(type: "integer", nullable: false),
                    min_stay_nights = table.Column<int>(type: "integer", nullable: true),
                    max_stay_nights = table.Column<int>(type: "integer", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    latitude = table.Column<double>(type: "numeric(9,6)", nullable: true),
                    longitude = table.Column<double>(type: "numeric(9,6)", nullable: true),
                    address_visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    short_stay = table.Column<bool>(type: "boolean", nullable: false),
                    long_stay = table.Column<bool>(type: "boolean", nullable: false),
                    student_friendly = table.Column<bool>(type: "boolean", nullable: false),
                    family_friendly = table.Column<bool>(type: "boolean", nullable: false),
                    shared = table.Column<bool>(type: "boolean", nullable: false),
                    roommate_wanted = table.Column<bool>(type: "boolean", nullable: false),
                    roommate_gender = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    roommate_occupation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    roommate_min_age = table.Column<int>(type: "integer", nullable: true),
                    roommate_max_age = table.Column<int>(type: "integer", nullable: true),
                    roommate_smoking = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    roommate_pets = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    roommate_noise = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    roommate_nationality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    roommate_languages = table.Column<string>(type: "character varying(610)", maxLength: 610, nullable: true),
                    LeaseType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FurnishedStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Parking = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SmokingPolicy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PetPolicy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verification_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    unique_view_count = table.Column<long>(type: "bigint", nullable: false),
                    save_count = table.Column<long>(type: "bigint", nullable: false),
                    viewing_request_count = table.Column<long>(type: "bigint", nullable: false),
                    reservation_count = table.Column<long>(type: "bigint", nullable: false),
                    enquiry_count = table.Column<long>(type: "bigint", nullable: false),
                    RequiresViewingBeforeReservation = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OccupiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_housing_listings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_neighborhood_insight_endorsements",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InsightId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_neighborhood_insight_endorsements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_neighborhood_insights",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EndorsementCount = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_neighborhood_insights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_neighborhoods",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RegionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AreaPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    overview = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    safety_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    transport_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cost_of_living_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    community_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    climate_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    walkability = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    family_friendliness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    student_friendliness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    nightlife = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    noise_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accessibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    parking_availability = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cost_of_living = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    internet_availability = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedInsightCount = table.Column<int>(type: "integer", nullable: false),
                    ListingCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_housing_neighborhoods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_reports",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_reservations",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeekerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    move_in_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    move_out_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hold_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    agreed_rent = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    agreed_deposit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    rent_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lease_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occupants = table.Column<int>(type: "integer", nullable: false),
                    ViewingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseSignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OccupiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_housing_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_viewings",
                schema: "housing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeekerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_virtual = table.Column<bool>(type: "boolean", nullable: false),
                    meeting_place = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LandlordFailedToAttend = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_housing_viewings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_analytics_daily",
                schema: "housing",
                columns: table => new
                {
                    MetricId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    UniqueViews = table.Column<long>(type: "bigint", nullable: false),
                    Saves = table.Column<long>(type: "bigint", nullable: false),
                    ViewingRequests = table.Column<long>(type: "bigint", nullable: false),
                    Reservations = table.Column<long>(type: "bigint", nullable: false),
                    HousingAnalyticsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_analytics_daily", x => x.MetricId);
                    table.ForeignKey(
                        name: "FK_housing_listing_analytics_daily_housing_listing_analytics_H~",
                        column: x => x.HousingAnalyticsId,
                        principalSchema: "housing",
                        principalTable: "housing_listing_analytics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_amenities",
                schema: "housing",
                columns: table => new
                {
                    AmenityKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_amenities", x => new { x.HousingListingId, x.AmenityKey });
                    table.ForeignKey(
                        name: "FK_housing_listing_amenities_housing_listings_HousingListingId",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_guides",
                schema: "housing",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_guides", x => new { x.HousingListingId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_housing_listing_guides_housing_listings_HousingListingId",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_house_rules",
                schema: "housing",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_house_rules", x => x.RuleId);
                    table.ForeignKey(
                        name: "FK_housing_listing_house_rules_housing_listings_HousingListing~",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_media",
                schema: "housing",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_media", x => new { x.HousingListingId, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_housing_listing_media_housing_listings_HousingListingId",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_moderation_notes",
                schema: "housing",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_moderation_notes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_housing_listing_moderation_notes_housing_listings_HousingLi~",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_listing_providers",
                schema: "housing",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    HousingListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_listing_providers", x => new { x.HousingListingId, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_housing_listing_providers_housing_listings_HousingListingId",
                        column: x => x.HousingListingId,
                        principalSchema: "housing",
                        principalTable: "housing_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_neighborhood_languages",
                schema: "housing",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NeighborhoodProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prominence = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_neighborhood_languages", x => new { x.NeighborhoodProfileId, x.Name });
                    table.ForeignKey(
                        name: "FK_housing_neighborhood_languages_housing_neighborhoods_Neighb~",
                        column: x => x.NeighborhoodProfileId,
                        principalSchema: "housing",
                        principalTable: "housing_neighborhoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_reservation_history",
                schema: "housing",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HousingReservationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_reservation_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_housing_reservation_history_housing_reservations_HousingRes~",
                        column: x => x.HousingReservationId,
                        principalSchema: "housing",
                        principalTable: "housing_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "housing_viewing_history",
                schema: "housing",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HousingViewingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_viewing_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_housing_viewing_history_housing_viewings_HousingViewingId",
                        column: x => x.HousingViewingId,
                        principalSchema: "housing",
                        principalTable: "housing_viewings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_housing_accommodation_types_active",
                schema: "housing",
                table: "housing_accommodation_types",
                columns: new[] { "IsActive", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_accommodation_types_key",
                schema: "housing",
                table: "housing_accommodation_types",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_amenities_accessibility",
                schema: "housing",
                table: "housing_amenities",
                column: "IsAccessibilityFeature");

            migrationBuilder.CreateIndex(
                name: "ix_housing_amenities_active_group",
                schema: "housing",
                table: "housing_amenities",
                columns: new[] { "IsActive", "Group", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_amenities_key",
                schema: "housing",
                table: "housing_amenities",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_country_status",
                schema: "housing",
                table: "housing_landlords",
                columns: new[] { "PrimaryCountryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_kind",
                schema: "housing",
                table: "housing_landlords",
                column: "landlord_kind");

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_owner",
                schema: "housing",
                table: "housing_landlords",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_provider",
                schema: "housing",
                table: "housing_landlords",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_slug",
                schema: "housing",
                table: "housing_landlords",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_landlords_verification",
                schema: "housing",
                table: "housing_landlords",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listing_amenities_key",
                schema: "housing",
                table: "housing_listing_amenities",
                column: "AmenityKey");

            migrationBuilder.CreateIndex(
                name: "ix_housing_analytics_country_type",
                schema: "housing",
                table: "housing_listing_analytics",
                columns: new[] { "CountryCode", "AccommodationTypeKey" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_analytics_landlord",
                schema: "housing",
                table: "housing_listing_analytics",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_analytics_listing",
                schema: "housing",
                table: "housing_listing_analytics",
                column: "ListingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_analytics_market",
                schema: "housing",
                table: "housing_listing_analytics",
                columns: new[] { "NeighborhoodAreaId", "AccommodationTypeKey", "FirstOccupiedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_analytics_daily_day",
                schema: "housing",
                table: "housing_listing_analytics_daily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_housing_listing_analytics_daily_HousingAnalyticsId",
                schema: "housing",
                table: "housing_listing_analytics_daily",
                column: "HousingAnalyticsId");

            migrationBuilder.CreateIndex(
                name: "IX_housing_listing_house_rules_HousingListingId",
                schema: "housing",
                table: "housing_listing_house_rules",
                column: "HousingListingId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listing_media_asset",
                schema: "housing",
                table: "housing_listing_media",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_housing_listing_moderation_notes_HousingListingId",
                schema: "housing",
                table: "housing_listing_moderation_notes",
                column: "HousingListingId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listing_providers_provider",
                schema: "housing",
                table: "housing_listing_providers",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_area_path",
                schema: "housing",
                table: "housing_listings",
                column: "area_path");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_available_from",
                schema: "housing",
                table: "housing_listings",
                column: "available_from");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_capacity",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "bedrooms", "bathrooms" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_country_city",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "country_code", "city_name" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_discovery",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "Status", "Visibility", "ModerationState" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_landlord_status",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "LandlordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_neighborhood",
                schema: "housing",
                table: "housing_listings",
                column: "neighborhood_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_published_at",
                schema: "housing",
                table: "housing_listings",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_rent",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "currency_code", "rent_period", "rent_amount" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_report_count",
                schema: "housing",
                table: "housing_listings",
                column: "ReportCount");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_slug",
                schema: "housing",
                table: "housing_listings",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_type",
                schema: "housing",
                table: "housing_listings",
                column: "AccommodationTypeKey");

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_verification",
                schema: "housing",
                table: "housing_listings",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_housing_insight_endorsements_unique",
                schema: "housing",
                table: "housing_neighborhood_insight_endorsements",
                columns: new[] { "InsightId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_insights_area_status",
                schema: "housing",
                table: "housing_neighborhood_insights",
                columns: new[] { "AreaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_insights_author_fingerprint",
                schema: "housing",
                table: "housing_neighborhood_insights",
                columns: new[] { "AuthorId", "AreaId", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_insights_endorsements",
                schema: "housing",
                table: "housing_neighborhood_insights",
                column: "EndorsementCount");

            migrationBuilder.CreateIndex(
                name: "ix_housing_insights_profile_status",
                schema: "housing",
                table: "housing_neighborhood_insights",
                columns: new[] { "ProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhood_languages_name",
                schema: "housing",
                table: "housing_neighborhood_languages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhoods_area",
                schema: "housing",
                table: "housing_neighborhoods",
                column: "AreaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhoods_area_path",
                schema: "housing",
                table: "housing_neighborhoods",
                column: "AreaPath");

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhoods_country_slug",
                schema: "housing",
                table: "housing_neighborhoods",
                columns: new[] { "CountryCode", "Slug" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhoods_listing_count",
                schema: "housing",
                table: "housing_neighborhoods",
                column: "ListingCount");

            migrationBuilder.CreateIndex(
                name: "ix_housing_reports_landlord_status",
                schema: "housing",
                table: "housing_reports",
                columns: new[] { "LandlordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_reports_listing_reporter",
                schema: "housing",
                table: "housing_reports",
                columns: new[] { "ListingId", "ReportedBy" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_housing_reports_status_reason",
                schema: "housing",
                table: "housing_reports",
                columns: new[] { "Status", "Reason" });

            migrationBuilder.CreateIndex(
                name: "IX_housing_reservation_history_HousingReservationId",
                schema: "housing",
                table: "housing_reservation_history",
                column: "HousingReservationId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_reservations_hold_expires",
                schema: "housing",
                table: "housing_reservations",
                column: "hold_expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_housing_reservations_landlord_status",
                schema: "housing",
                table: "housing_reservations",
                columns: new[] { "LandlordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_reservations_listing_seeker",
                schema: "housing",
                table: "housing_reservations",
                columns: new[] { "ListingId", "SeekerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_reservations_seeker_status",
                schema: "housing",
                table: "housing_reservations",
                columns: new[] { "SeekerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_housing_viewing_history_HousingViewingId",
                schema: "housing",
                table: "housing_viewing_history",
                column: "HousingViewingId");

            migrationBuilder.CreateIndex(
                name: "ix_housing_viewings_confirmed_at",
                schema: "housing",
                table: "housing_viewings",
                column: "confirmed_at");

            migrationBuilder.CreateIndex(
                name: "ix_housing_viewings_landlord_status",
                schema: "housing",
                table: "housing_viewings",
                columns: new[] { "LandlordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_viewings_listing_seeker",
                schema: "housing",
                table: "housing_viewings",
                columns: new[] { "ListingId", "SeekerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_viewings_seeker_status",
                schema: "housing",
                table: "housing_viewings",
                columns: new[] { "SeekerUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "housing_accommodation_types",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_amenities",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_landlords",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_amenities",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_analytics_daily",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_guides",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_house_rules",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_media",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_moderation_notes",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_providers",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_neighborhood_insight_endorsements",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_neighborhood_insights",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_neighborhood_languages",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_reports",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_reservation_history",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_viewing_history",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listing_analytics",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_listings",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_neighborhoods",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_reservations",
                schema: "housing");

            migrationBuilder.DropTable(
                name: "housing_viewings",
                schema: "housing");
        }
    }
}

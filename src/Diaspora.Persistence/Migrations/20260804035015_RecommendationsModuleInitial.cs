using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationsModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recommendations");

            migrationBuilder.CreateTable(
                name: "personalization_preferences",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPersonalizationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShareActivitySignals = table.Column<bool>(type: "boolean", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PreferredCountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personalization_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "personalization_profiles",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PrimaryCountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    PrimaryCityName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PrimaryNeighborhoodName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TrustLevel = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    HasCompletedOnboarding = table.Column<bool>(type: "boolean", nullable: false),
                    IsColdStart = table.Column<bool>(type: "boolean", nullable: false),
                    SignalCount = table.Column<int>(type: "integer", nullable: false),
                    Completeness = table.Column<int>(type: "integer", nullable: false),
                    LastSignalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastBuiltAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRebuiltAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personalization_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ranking_strategies",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RunsOnColdStart = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumScore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranking_strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_analytics",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GeneratedSetCount = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedItemCount = table.Column<long>(type: "bigint", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false),
                    AcceptCount = table.Column<long>(type: "bigint", nullable: false),
                    DismissCount = table.Column<long>(type: "bigint", nullable: false),
                    BookmarkCount = table.Column<long>(type: "bigint", nullable: false),
                    ColdStartSetCount = table.Column<long>(type: "bigint", nullable: false),
                    ColdStartAcceptCount = table.Column<long>(type: "bigint", nullable: false),
                    PolicyExclusionCount = table.Column<long>(type: "bigint", nullable: false),
                    DistinctTargetCount = table.Column<long>(type: "bigint", nullable: false),
                    ProfileCompletenessSum = table.Column<long>(type: "bigint", nullable: false),
                    GenerationMillisecondsSum = table.Column<long>(type: "bigint", nullable: false),
                    GenerationMillisecondsMax = table.Column<int>(type: "integer", nullable: false),
                    SignalFreshnessHoursSum = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_candidates",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MediaReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RegionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CityName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NeighborhoodName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    PopularityScore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    QualityScore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    EngagementCount = table.Column<long>(type: "bigint", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MinimumTrustLevel = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_feedback",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SetId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrategyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuppressedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RolledUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_feedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_policies",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Effect = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    DemotionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Surfaces = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TargetTypes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_sets",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsColdStart = table.Column<bool>(type: "boolean", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    PolicyExclusionCount = table.Column<int>(type: "integer", nullable: false),
                    StrategyKeys = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    GenerationMilliseconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_signals",
                schema: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    KeyKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    country_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    city_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    neighborhood_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    language_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceEventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "preference_blocked_targets",
                schema: "recommendations",
                columns: table => new
                {
                    BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PersonalizationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preference_blocked_targets", x => x.BlockId);
                    table.ForeignKey(
                        name: "FK_preference_blocked_targets_personalization_preferences_Pers~",
                        column: x => x.PersonalizationPreferenceId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preference_categories",
                schema: "recommendations",
                columns: table => new
                {
                    PreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    PersonalizationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preference_categories", x => x.PreferenceId);
                    table.ForeignKey(
                        name: "FK_preference_categories_personalization_preferences_Personali~",
                        column: x => x.PersonalizationPreferenceId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preference_favorite_locations",
                schema: "recommendations",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PersonalizationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preference_favorite_locations", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_preference_favorite_locations_personalization_preferences_P~",
                        column: x => x.PersonalizationPreferenceId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preference_surfaces",
                schema: "recommendations",
                columns: table => new
                {
                    PreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PersonalizationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preference_surfaces", x => x.PreferenceId);
                    table.ForeignKey(
                        name: "FK_preference_surfaces_personalization_preferences_Personaliza~",
                        column: x => x.PersonalizationPreferenceId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_activity_summaries",
                schema: "recommendations",
                columns: table => new
                {
                    SummaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    SaveCount = table.Column<int>(type: "integer", nullable: false),
                    ApplyCount = table.Column<int>(type: "integer", nullable: false),
                    TransactCount = table.Column<int>(type: "integer", nullable: false),
                    DismissCount = table.Column<int>(type: "integer", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PersonalizationProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_activity_summaries", x => x.SummaryId);
                    table.ForeignKey(
                        name: "FK_profile_activity_summaries_personalization_profiles_Persona~",
                        column: x => x.PersonalizationProfileId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_interests",
                schema: "recommendations",
                columns: table => new
                {
                    InterestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Affinity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsExplicit = table.Column<bool>(type: "boolean", nullable: false),
                    PersonalizationProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_interests", x => x.InterestId);
                    table.ForeignKey(
                        name: "FK_profile_interests_personalization_profiles_PersonalizationP~",
                        column: x => x.PersonalizationProfileId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_location_affinities",
                schema: "recommendations",
                columns: table => new
                {
                    AffinityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Affinity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    PersonalizationProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_location_affinities", x => x.AffinityId);
                    table.ForeignKey(
                        name: "FK_profile_location_affinities_personalization_profiles_Person~",
                        column: x => x.PersonalizationProfileId,
                        principalSchema: "recommendations",
                        principalTable: "personalization_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ranking_strategy_parameters",
                schema: "recommendations",
                columns: table => new
                {
                    ParameterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RankingStrategyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranking_strategy_parameters", x => x.ParameterId);
                    table.ForeignKey(
                        name: "FK_ranking_strategy_parameters_ranking_strategies_RankingStrat~",
                        column: x => x.RankingStrategyId,
                        principalSchema: "recommendations",
                        principalTable: "ranking_strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_analytics_breakdowns",
                schema: "recommendations",
                columns: table => new
                {
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GeneratedCount = table.Column<long>(type: "bigint", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false),
                    AcceptCount = table.Column<long>(type: "bigint", nullable: false),
                    DismissCount = table.Column<long>(type: "bigint", nullable: false),
                    RecommendationAnalyticsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_analytics_breakdowns", x => x.BreakdownId);
                    table.ForeignKey(
                        name: "FK_recommendation_analytics_breakdowns_recommendation_analytic~",
                        column: x => x.RecommendationAnalyticsId,
                        principalSchema: "recommendations",
                        principalTable: "recommendation_analytics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_candidate_attributes",
                schema: "recommendations",
                columns: table => new
                {
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecommendationCandidateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_candidate_attributes", x => x.AttributeId);
                    table.ForeignKey(
                        name: "FK_recommendation_candidate_attributes_recommendation_candidat~",
                        column: x => x.RecommendationCandidateId,
                        principalSchema: "recommendations",
                        principalTable: "recommendation_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_policy_parameters",
                schema: "recommendations",
                columns: table => new
                {
                    ParameterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RecommendationPolicyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_policy_parameters", x => x.ParameterId);
                    table.ForeignKey(
                        name: "FK_recommendation_policy_parameters_recommendation_policies_Re~",
                        column: x => x.RecommendationPolicyId,
                        principalSchema: "recommendations",
                        principalTable: "recommendation_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_items",
                schema: "recommendations",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PrimaryStrategyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_resource_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason_text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    confidence = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    signal_kinds = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RecommendationSetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_items", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_recommendation_items_recommendation_sets_RecommendationSetId",
                        column: x => x.RecommendationSetId,
                        principalSchema: "recommendations",
                        principalTable: "recommendation_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_item_contributions",
                schema: "recommendations",
                columns: table => new
                {
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RawScore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    WeightedScore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RecommendationItemItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_item_contributions", x => x.ContributionId);
                    table.ForeignKey(
                        name: "FK_recommendation_item_contributions_recommendation_items_Reco~",
                        column: x => x.RecommendationItemItemId,
                        principalSchema: "recommendations",
                        principalTable: "recommendation_items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_personalization_preferences_user",
                schema: "recommendations",
                table: "personalization_preferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personalization_profiles_stale",
                schema: "recommendations",
                table: "personalization_profiles",
                column: "LastBuiltAt");

            migrationBuilder.CreateIndex(
                name: "ix_personalization_profiles_user",
                schema: "recommendations",
                table: "personalization_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preference_blocks_unique",
                schema: "recommendations",
                table: "preference_blocked_targets",
                columns: new[] { "PersonalizationPreferenceId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preference_categories_unique",
                schema: "recommendations",
                table: "preference_categories",
                columns: new[] { "PersonalizationPreferenceId", "CategoryKey", "TargetType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preference_locations_unique",
                schema: "recommendations",
                table: "preference_favorite_locations",
                columns: new[] { "PersonalizationPreferenceId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preference_surfaces_unique",
                schema: "recommendations",
                table: "preference_surfaces",
                columns: new[] { "PersonalizationPreferenceId", "Surface" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_activity_unique",
                schema: "recommendations",
                table: "profile_activity_summaries",
                columns: new[] { "PersonalizationProfileId", "TargetType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_interests_unique",
                schema: "recommendations",
                table: "profile_interests",
                columns: new[] { "PersonalizationProfileId", "Kind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_locations_unique",
                schema: "recommendations",
                table: "profile_location_affinities",
                columns: new[] { "PersonalizationProfileId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ranking_strategies_key_surface",
                schema: "recommendations",
                table: "ranking_strategies",
                columns: new[] { "Key", "Surface" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ranking_strategies_surface",
                schema: "recommendations",
                table: "ranking_strategies",
                columns: new[] { "Surface", "IsEnabled", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_strategy_parameters_unique",
                schema: "recommendations",
                table: "ranking_strategy_parameters",
                columns: new[] { "RankingStrategyId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_analytics_day",
                schema: "recommendations",
                table: "recommendation_analytics",
                columns: new[] { "Day", "Surface" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analytics_breakdowns_unique",
                schema: "recommendations",
                table: "recommendation_analytics_breakdowns",
                columns: new[] { "RecommendationAnalyticsId", "Dimension", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_attributes_unique",
                schema: "recommendations",
                table: "recommendation_candidate_attributes",
                columns: new[] { "RecommendationCandidateId", "Kind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_category",
                schema: "recommendations",
                table: "recommendation_candidates",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_owner",
                schema: "recommendations",
                table: "recommendation_candidates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_pool",
                schema: "recommendations",
                table: "recommendation_candidates",
                columns: new[] { "IsActive", "CountryCode", "PopularityScore" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_recency",
                schema: "recommendations",
                table: "recommendation_candidates",
                columns: new[] { "IsActive", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_target",
                schema: "recommendations",
                table: "recommendation_candidates",
                columns: new[] { "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_candidates_withdrawn",
                schema: "recommendations",
                table: "recommendation_candidates",
                columns: new[] { "IsActive", "ModifiedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_feedback_suppression",
                schema: "recommendations",
                table: "recommendation_feedback",
                columns: new[] { "UserId", "Kind", "SuppressedUntil" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_feedback_unrolled",
                schema: "recommendations",
                table: "recommendation_feedback",
                columns: new[] { "RolledUpAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_feedback_user",
                schema: "recommendations",
                table: "recommendation_feedback",
                columns: new[] { "UserId", "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_item_contributions_RecommendationItemItemId",
                schema: "recommendations",
                table: "recommendation_item_contributions",
                column: "RecommendationItemItemId");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_position",
                schema: "recommendations",
                table: "recommendation_items",
                columns: new[] { "RecommendationSetId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_policies_enabled",
                schema: "recommendations",
                table: "recommendation_policies",
                columns: new[] { "IsEnabled", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_policies_key",
                schema: "recommendations",
                table: "recommendation_policies",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_parameters_unique",
                schema: "recommendations",
                table: "recommendation_policy_parameters",
                columns: new[] { "RecommendationPolicyId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_sets_fresh",
                schema: "recommendations",
                table: "recommendation_sets",
                columns: new[] { "UserId", "Surface", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_sets_generated",
                schema: "recommendations",
                table: "recommendation_sets",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "ix_user_signals_expiry",
                schema: "recommendations",
                table: "user_signals",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_user_signals_recent",
                schema: "recommendations",
                table: "user_signals",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_signals_target",
                schema: "recommendations",
                table: "user_signals",
                columns: new[] { "UserId", "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "ix_user_signals_unprocessed",
                schema: "recommendations",
                table: "user_signals",
                columns: new[] { "UserId", "ProcessedAt", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "preference_blocked_targets",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "preference_categories",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "preference_favorite_locations",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "preference_surfaces",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "profile_activity_summaries",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "profile_interests",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "profile_location_affinities",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "ranking_strategy_parameters",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_analytics_breakdowns",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_candidate_attributes",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_feedback",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_item_contributions",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_policy_parameters",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "user_signals",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "personalization_preferences",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "personalization_profiles",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "ranking_strategies",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_analytics",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_candidates",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_items",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_policies",
                schema: "recommendations");

            migrationBuilder.DropTable(
                name: "recommendation_sets",
                schema: "recommendations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeDirectoryInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_categories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_knowledge_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_analytics",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DirectoryViews = table.Column<long>(type: "bigint", nullable: false),
                    CategoryViews = table.Column<long>(type: "bigint", nullable: false),
                    GuideDiscoveries = table.Column<long>(type: "bigint", nullable: false),
                    GuideCompletions = table.Column<long>(type: "bigint", nullable: false),
                    Searches = table.Column<long>(type: "bigint", nullable: false),
                    LearningPathStarts = table.Column<long>(type: "bigint", nullable: false),
                    LearningPathCompletions = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_context_summaries",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_context_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_directories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LanguagesCsv = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_knowledge_country_directories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_learning_paths",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_learning_paths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_recommendations",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceGuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_search_terms",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SearchCount = table.Column<long>(type: "bigint", nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    LastSearchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_search_terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_category_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    KnowledgeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_category_localizations", x => new { x.KnowledgeCategoryId, x.Locale });
                    table.ForeignKey(
                        name: "FK_knowledge_category_localizations_knowledge_categories_Knowl~",
                        column: x => x.KnowledgeCategoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_category_provider_categories",
                schema: "guides",
                columns: table => new
                {
                    ServiceCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KnowledgeCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_category_provider_categories", x => new { x.KnowledgeCategoryId, x.ServiceCategoryKey });
                    table.ForeignKey(
                        name: "FK_knowledge_category_provider_categories_knowledge_categories~",
                        column: x => x.KnowledgeCategoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_category_sources",
                schema: "guides",
                columns: table => new
                {
                    GuideCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KnowledgeCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_category_sources", x => new { x.KnowledgeCategoryId, x.GuideCategoryKey });
                    table.ForeignKey(
                        name: "FK_knowledge_category_sources_knowledge_categories_KnowledgeCa~",
                        column: x => x.KnowledgeCategoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_category_views",
                schema: "guides",
                columns: table => new
                {
                    KnowledgeCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryKnowledgeAnalyticsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    LastViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_category_views", x => new { x.CountryKnowledgeAnalyticsId, x.KnowledgeCategoryKey });
                    table.ForeignKey(
                        name: "FK_knowledge_category_views_knowledge_country_analytics_Countr~",
                        column: x => x.CountryKnowledgeAnalyticsId,
                        principalSchema: "guides",
                        principalTable: "knowledge_country_analytics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_context_counters",
                schema: "guides",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryContextSummaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_context_counters", x => new { x.CountryContextSummaryId, x.Kind });
                    table.ForeignKey(
                        name: "FK_knowledge_country_context_counters_knowledge_country_contex~",
                        column: x => x.CountryContextSummaryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_country_context_summaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_directory_entries",
                schema: "guides",
                columns: table => new
                {
                    KnowledgeCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryKnowledgeDirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    LabelOverride = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_directory_entries", x => new { x.CountryKnowledgeDirectoryId, x.KnowledgeCategoryKey });
                    table.ForeignKey(
                        name: "FK_knowledge_country_directory_entries_knowledge_country_direc~",
                        column: x => x.CountryKnowledgeDirectoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_country_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_directory_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CountryKnowledgeDirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_directory_localizations", x => new { x.CountryKnowledgeDirectoryId, x.Locale });
                    table.ForeignKey(
                        name: "FK_knowledge_country_directory_localizations_knowledge_country~",
                        column: x => x.CountryKnowledgeDirectoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_country_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_country_featured_guides",
                schema: "guides",
                columns: table => new
                {
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FeaturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CountryKnowledgeDirectoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_country_featured_guides", x => x.FeatureId);
                    table.ForeignKey(
                        name: "FK_knowledge_country_featured_guides_knowledge_country_directo~",
                        column: x => x.CountryKnowledgeDirectoryId,
                        principalSchema: "guides",
                        principalTable: "knowledge_country_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_learning_path_steps",
                schema: "guides",
                columns: table => new
                {
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnowledgeCategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    LearningPathId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_learning_path_steps", x => x.StepId);
                    table.ForeignKey(
                        name: "FK_knowledge_learning_path_steps_knowledge_learning_paths_Lear~",
                        column: x => x.LearningPathId,
                        principalSchema: "guides",
                        principalTable: "knowledge_learning_paths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_recommended_reads",
                schema: "guides",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_recommended_reads", x => new { x.KnowledgeRecommendationId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_knowledge_recommended_reads_knowledge_recommendations_Knowl~",
                        column: x => x.KnowledgeRecommendationId,
                        principalSchema: "guides",
                        principalTable: "knowledge_recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_categories_order",
                schema: "guides",
                table: "knowledge_categories",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_categories_key",
                schema: "guides",
                table: "knowledge_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_category_sources_guide_category",
                schema: "guides",
                table: "knowledge_category_sources",
                column: "GuideCategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_country_analytics_views",
                schema: "guides",
                table: "knowledge_country_analytics",
                column: "DirectoryViews");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_country_analytics_country",
                schema: "guides",
                table: "knowledge_country_analytics",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_country_context_country",
                schema: "guides",
                table: "knowledge_country_context_summaries",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_country_directories_published",
                schema: "guides",
                table: "knowledge_country_directories",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_country_directories_country",
                schema: "guides",
                table: "knowledge_country_directories",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_directory_entries_category",
                schema: "guides",
                table: "knowledge_country_directory_entries",
                column: "KnowledgeCategoryKey");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_country_featured_guides_CountryKnowledgeDirectory~",
                schema: "guides",
                table: "knowledge_country_featured_guides",
                column: "CountryKnowledgeDirectoryId");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_featured_guides_guide",
                schema: "guides",
                table: "knowledge_country_featured_guides",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_learning_path_steps_guide",
                schema: "guides",
                table: "knowledge_learning_path_steps",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_learning_path_steps_LearningPathId",
                schema: "guides",
                table: "knowledge_learning_path_steps",
                column: "LearningPathId");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_learning_paths_country_status",
                schema: "guides",
                table: "knowledge_learning_paths",
                columns: new[] { "CountryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_learning_paths_country_key",
                schema: "guides",
                table: "knowledge_learning_paths",
                columns: new[] { "CountryCode", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_recommendations_source",
                schema: "guides",
                table: "knowledge_recommendations",
                column: "SourceGuideId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_recommended_reads_guide",
                schema: "guides",
                table: "knowledge_recommended_reads",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_search_terms_country_count",
                schema: "guides",
                table: "knowledge_search_terms",
                columns: new[] { "CountryCode", "SearchCount" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_search_terms_country_results",
                schema: "guides",
                table: "knowledge_search_terms",
                columns: new[] { "CountryCode", "ResultCount" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_search_terms_country_term",
                schema: "guides",
                table: "knowledge_search_terms",
                columns: new[] { "CountryCode", "Term" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_category_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_category_provider_categories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_category_sources",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_category_views",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_context_counters",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_directory_entries",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_directory_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_featured_guides",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_learning_path_steps",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_recommended_reads",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_search_terms",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_categories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_analytics",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_context_summaries",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_country_directories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_learning_paths",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "knowledge_recommendations",
                schema: "guides");
        }
    }
}

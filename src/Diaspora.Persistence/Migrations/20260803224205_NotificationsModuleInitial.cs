using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "notification_analytics",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedCount = table.Column<long>(type: "bigint", nullable: false),
                    QueuedCount = table.Column<long>(type: "bigint", nullable: false),
                    DeliveredCount = table.Column<long>(type: "bigint", nullable: false),
                    OpenedCount = table.Column<long>(type: "bigint", nullable: false),
                    ClickedCount = table.Column<long>(type: "bigint", nullable: false),
                    DismissedCount = table.Column<long>(type: "bigint", nullable: false),
                    ArchivedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    SuppressedCount = table.Column<long>(type: "bigint", nullable: false),
                    DigestedCount = table.Column<long>(type: "bigint", nullable: false),
                    RetryCount = table.Column<long>(type: "bigint", nullable: false),
                    MinutesToOpenTotal = table.Column<double>(type: "double precision", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_categories",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    DigestByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerModule = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_devices",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FriendlyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastDeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    SuppressedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuppressionReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_notification_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_digests",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_notification_digests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quiet_hours_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    quiet_hours_start = table.Column<int>(type: "integer", nullable: false),
                    quiet_hours_end = table.Column<int>(type: "integer", nullable: false),
                    digest_frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    digest_hour = table.Column<int>(type: "integer", nullable: false),
                    digest_day_of_week = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DoNotDisturb = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsMarketing = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_rules",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Audience = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Policy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChannelMask = table.Column<int>(type: "integer", nullable: false),
                    Offset = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RetentionPeriod = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DeduplicationWindow = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerModule = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RouteTemplate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RetentionPeriod = table.Column<TimeSpan>(type: "interval", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_event_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    subject_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    short_body = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    image_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    link_entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    link_entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusBeforeArchive = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ArchivedFor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    clicked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    opened_on_channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsStarred = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GroupKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_inboxes",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ArchivedCount = table.Column<int>(type: "integer", nullable: false),
                    PinnedCount = table.Column<int>(type: "integer", nullable: false),
                    StarredCount = table.Column<int>(type: "integer", nullable: false),
                    LastViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastNotificationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_inboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_digest_entries",
                schema: "notifications",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Route = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationDigestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_digest_entries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_notification_digest_entries_notification_digests_Notificati~",
                        column: x => x.NotificationDigestId,
                        principalSchema: "notifications",
                        principalTable: "notification_digests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_category_preferences",
                schema: "notifications",
                columns: table => new
                {
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NotificationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InApp = table.Column<bool>(type: "boolean", nullable: false),
                    Push = table.Column<bool>(type: "boolean", nullable: false),
                    Email = table.Column<bool>(type: "boolean", nullable: false),
                    UseDigest = table.Column<bool>(type: "boolean", nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_category_preferences", x => new { x.NotificationPreferenceId, x.CategoryKey });
                    table.ForeignKey(
                        name: "FK_notification_category_preferences_notification_preferences_~",
                        column: x => x.NotificationPreferenceId,
                        principalSchema: "notifications",
                        principalTable: "notification_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_channel_preferences",
                schema: "notifications",
                columns: table => new
                {
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotificationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_channel_preferences", x => new { x.NotificationPreferenceId, x.Channel });
                    table.ForeignKey(
                        name: "FK_notification_channel_preferences_notification_preferences_N~",
                        column: x => x.NotificationPreferenceId,
                        principalSchema: "notifications",
                        principalTable: "notification_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_template_actions",
                schema: "notifications",
                columns: table => new
                {
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RouteTemplate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Style = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template_actions", x => x.ActionId);
                    table.ForeignKey(
                        name: "FK_notification_template_actions_notification_templates_Notifi~",
                        column: x => x.NotificationTemplateId,
                        principalSchema: "notifications",
                        principalTable: "notification_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_template_localizations",
                schema: "notifications",
                columns: table => new
                {
                    LocalizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ShortBody = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PluralBody = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template_localizations", x => x.LocalizationId);
                    table.ForeignKey(
                        name: "FK_notification_template_localizations_notification_templates_~",
                        column: x => x.NotificationTemplateId,
                        principalSchema: "notifications",
                        principalTable: "notification_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_template_variables",
                schema: "notifications",
                columns: table => new
                {
                    VariableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Sample = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template_variables", x => x.VariableId);
                    table.ForeignKey(
                        name: "FK_notification_template_variables_notification_templates_Noti~",
                        column: x => x.NotificationTemplateId,
                        principalSchema: "notifications",
                        principalTable: "notification_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_template_variants",
                schema: "notifications",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template_variants", x => x.VariantId);
                    table.ForeignKey(
                        name: "FK_notification_template_variants_notification_templates_Notif~",
                        column: x => x.NotificationTemplateId,
                        principalSchema: "notifications",
                        principalTable: "notification_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_actions",
                schema: "notifications",
                columns: table => new
                {
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Style = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    route = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    link_entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    link_entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_actions", x => x.ActionId);
                    table.ForeignKey(
                        name: "FK_notification_actions_notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                schema: "notifications",
                columns: table => new
                {
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliverAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DigestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.DeliveryId);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery_attempts",
                schema: "notifications",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Detail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery_attempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_notification_delivery_attempts_notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_inbox_counters",
                schema: "notifications",
                columns: table => new
                {
                    CounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    UserInboxId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_inbox_counters", x => x.CounterId);
                    table.ForeignKey(
                        name: "FK_user_inbox_counters_user_inboxes_UserInboxId",
                        column: x => x.UserInboxId,
                        principalSchema: "notifications",
                        principalTable: "user_inboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_actions_NotificationId",
                schema: "notifications",
                table: "notification_actions",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "ix_notification_analytics_day",
                schema: "notifications",
                table: "notification_analytics",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "ix_notification_analytics_grain",
                schema: "notifications",
                table: "notification_analytics",
                columns: new[] { "Day", "CategoryKey", "Channel", "TemplateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_categories_key",
                schema: "notifications",
                table: "notification_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_categories_order",
                schema: "notifications",
                table: "notification_categories",
                columns: new[] { "IsActive", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_due",
                schema: "notifications",
                table: "notification_deliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_unique",
                schema: "notifications",
                table: "notification_deliveries",
                columns: new[] { "NotificationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_attempts_timeline",
                schema: "notifications",
                table: "notification_delivery_attempts",
                columns: new[] { "NotificationId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_devices_deliverable",
                schema: "notifications",
                table: "notification_devices",
                columns: new[] { "UserId", "Channel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_devices_identifier",
                schema: "notifications",
                table: "notification_devices",
                columns: new[] { "UserId", "DeviceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_devices_token",
                schema: "notifications",
                table: "notification_devices",
                columns: new[] { "UserId", "Channel", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_digest_entries_NotificationDigestId",
                schema: "notifications",
                table: "notification_digest_entries",
                column: "NotificationDigestId");

            migrationBuilder.CreateIndex(
                name: "ix_notification_digests_recipient",
                schema: "notifications",
                table: "notification_digests",
                columns: new[] { "RecipientUserId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_digests_window",
                schema: "notifications",
                table: "notification_digests",
                columns: new[] { "RecipientUserId", "Frequency", "WindowStart", "WindowEnd" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user",
                schema: "notifications",
                table: "notification_preferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_event",
                schema: "notifications",
                table: "notification_rules",
                columns: new[] { "EventName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_key",
                schema: "notifications",
                table: "notification_rules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_template_actions_NotificationTemplateId",
                schema: "notifications",
                table: "notification_template_actions",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_localizations_unique",
                schema: "notifications",
                table: "notification_template_localizations",
                columns: new[] { "NotificationTemplateId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_template_variables_NotificationTemplateId",
                schema: "notifications",
                table: "notification_template_variables",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_template_variants_NotificationTemplateId",
                schema: "notifications",
                table: "notification_template_variants",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_category",
                schema: "notifications",
                table: "notification_templates",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_key_version",
                schema: "notifications",
                table: "notification_templates",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_published",
                schema: "notifications",
                table: "notification_templates",
                columns: new[] { "Key", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_category",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "RecipientUserId", "CategoryKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deduplication",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "RecipientUserId", "TemplateKey", "GroupKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_expiry",
                schema: "notifications",
                table: "notifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_inbox",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "RecipientUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_inbox_counters_unique",
                schema: "notifications",
                table: "user_inbox_counters",
                columns: new[] { "UserInboxId", "CategoryKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_inboxes_stale",
                schema: "notifications",
                table: "user_inboxes",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "ix_user_inboxes_user",
                schema: "notifications",
                table: "user_inboxes",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_actions",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_analytics",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_categories",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_category_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_channel_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_deliveries",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_delivery_attempts",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_devices",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_digest_entries",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_rules",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_template_actions",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_template_localizations",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_template_variables",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_template_variants",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "user_inbox_counters",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_digests",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_templates",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "user_inboxes",
                schema: "notifications");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationsModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "conversations");

            migrationBuilder.CreateTable(
                name: "conversation_blocks",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    BlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_context_types",
                schema: "conversations",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    OwningModule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequiresAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsAttachments = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemManaged = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_context_types", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attachment_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    attachment_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attachment_thumbnail_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attachment_mime_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    attachment_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    attachment_width = table.Column<int>(type: "integer", nullable: true),
                    attachment_height = table.Column<int>(type: "integer", nullable: true),
                    attachment_duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    attachment_file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attachment_page_count = table.Column<int>(type: "integer", nullable: true),
                    attachment_caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reply_to_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reply_to_sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reply_to_preview = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reply_to_attachment_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SystemKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ClientMessageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_conversation_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    context_entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    context_entity_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    context_entity_subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    context_entity_image_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    context_entity_price = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    context_entity_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    context_entity_status_label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    context_deep_link = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    context_snapshot_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    campaign_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    referral_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    app_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastMessageSenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participants",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    MutedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    PinOrder = table.Column<int>(type: "integer", nullable: true),
                    LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    LastDeliveredMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastDeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversation_participants_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_blocks_blocked",
                schema: "conversations",
                table: "conversation_blocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "ux_conversation_blocks_pair",
                schema: "conversations",
                table: "conversation_blocks",
                columns: new[] { "BlockerUserId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_context_types_order",
                schema: "conversations",
                table: "conversation_context_types",
                columns: new[] { "IsActive", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_client_id",
                schema: "conversations",
                table: "conversation_messages",
                columns: new[] { "ConversationId", "ClientMessageId" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_history",
                schema: "conversations",
                table: "conversation_messages",
                columns: new[] { "ConversationId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_receipts",
                schema: "conversations",
                table: "conversation_messages",
                columns: new[] { "ConversationId", "SenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_inbox",
                schema: "conversations",
                table: "conversation_participants",
                columns: new[] { "UserId", "IsArchived", "LeftAt" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_pinned",
                schema: "conversations",
                table: "conversation_participants",
                columns: new[] { "UserId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_unread",
                schema: "conversations",
                table: "conversation_participants",
                columns: new[] { "UserId", "UnreadCount" });

            migrationBuilder.CreateIndex(
                name: "ux_conversation_participants_member",
                schema: "conversations",
                table: "conversation_participants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_activity",
                schema: "conversations",
                table: "conversations",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_reference",
                schema: "conversations",
                table: "conversations",
                columns: new[] { "context_type", "context_entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_status_activity",
                schema: "conversations",
                table: "conversations",
                columns: new[] { "Status", "LastActivityAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_blocks",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "conversation_context_types",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "conversation_messages",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "conversation_participants",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "conversations");
        }
    }
}

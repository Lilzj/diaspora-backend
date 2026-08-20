using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationAttentionMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InactivityNoticedAt",
                schema: "conversations",
                table: "conversation_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnreadNoticedAt",
                schema: "conversations",
                table: "conversation_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_unread_notice",
                schema: "conversations",
                table: "conversation_participants",
                columns: new[] { "UnreadCount", "UnreadNoticedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversation_participants_unread_notice",
                schema: "conversations",
                table: "conversation_participants");

            migrationBuilder.DropColumn(
                name: "InactivityNoticedAt",
                schema: "conversations",
                table: "conversation_participants");

            migrationBuilder.DropColumn(
                name: "UnreadNoticedAt",
                schema: "conversations",
                table: "conversation_participants");
        }
    }
}

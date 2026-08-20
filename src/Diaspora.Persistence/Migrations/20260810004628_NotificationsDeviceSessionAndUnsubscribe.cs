using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsDeviceSessionAndUnsubscribe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionDeviceId",
                schema: "notifications",
                table: "notification_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeSource",
                schema: "notifications",
                table: "notification_category_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnsubscribedAt",
                schema: "notifications",
                table: "notification_category_preferences",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionDeviceId",
                schema: "notifications",
                table: "notification_devices");

            migrationBuilder.DropColumn(
                name: "UnsubscribeSource",
                schema: "notifications",
                table: "notification_category_preferences");

            migrationBuilder.DropColumn(
                name: "UnsubscribedAt",
                schema: "notifications",
                table: "notification_category_preferences");
        }
    }
}

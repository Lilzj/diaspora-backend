using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedDeviceSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SharedDeviceFlaggedAt",
                schema: "creator",
                table: "creator_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_identifier",
                schema: "identity",
                table: "user_devices",
                column: "DeviceIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_devices_identifier",
                schema: "identity",
                table: "user_devices");

            migrationBuilder.DropColumn(
                name: "SharedDeviceFlaggedAt",
                schema: "creator",
                table: "creator_profiles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustSubmissionContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                schema: "trust",
                table: "verification_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                schema: "trust",
                table: "verification_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "trust",
                table: "verification_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                schema: "trust",
                table: "verification_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionMetadata",
                schema: "trust",
                table: "verification_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaId",
                schema: "trust",
                table: "verification_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_verification_requests_subject_type_status",
                schema: "trust",
                table: "verification_requests",
                columns: new[] { "SubjectType", "SubjectId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_verification_requests_correlation_id",
                schema: "trust",
                table: "verification_requests",
                column: "CorrelationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_verification_requests_subject_type_status",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropIndex(
                name: "ux_verification_requests_correlation_id",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "RequestedBy",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "SubmissionMetadata",
                schema: "trust",
                table: "verification_requests");

            migrationBuilder.DropColumn(
                name: "MediaId",
                schema: "trust",
                table: "verification_documents");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialB151GivingExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_campaign_updates",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    MediaReferences = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_campaign_updates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_campaigns",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Story = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CoverImageReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Gallery = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BeneficiaryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcceptedKinds = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    LocationLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    TargetMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CauseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CauseCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedTermsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_membership_payment_bindings",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ReannouncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReannounceCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_membership_payment_bindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_recurring_donations",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CauseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CauseCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    IntervalMonths = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NextChargeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastChargeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDonationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    ChargeCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OriginDonationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_recurring_donations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_campaign_fund_usage",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AllocatedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    SpentMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    WithdrawalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_campaign_fund_usage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_campaign_fund_usage_financial_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "financial",
                        principalTable: "financial_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_campaign_milestones",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    ReachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_campaign_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_campaign_milestones_financial_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "financial",
                        principalTable: "financial_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaign_fund_usage_campaign",
                schema: "financial",
                table: "financial_campaign_fund_usage",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaign_milestones_target",
                schema: "financial",
                table: "financial_campaign_milestones",
                columns: new[] { "CampaignId", "TargetMinorUnits" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaign_updates_campaign",
                schema: "financial",
                table: "financial_campaign_updates",
                columns: new[] { "CampaignId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaign_updates_idempotency",
                schema: "financial",
                table: "financial_campaign_updates",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaigns_cause",
                schema: "financial",
                table: "financial_campaigns",
                column: "CauseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaigns_idempotency",
                schema: "financial",
                table: "financial_campaigns",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaigns_organization",
                schema: "financial",
                table: "financial_campaigns",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaigns_organizer",
                schema: "financial",
                table: "financial_campaigns",
                columns: new[] { "OrganizerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_campaigns_status",
                schema: "financial",
                table: "financial_campaigns",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_membership_bindings_intent",
                schema: "financial",
                table: "financial_membership_payment_bindings",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_membership_bindings_membership",
                schema: "financial",
                table: "financial_membership_payment_bindings",
                columns: new[] { "MembershipId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_membership_bindings_reconciliation",
                schema: "financial",
                table: "financial_membership_payment_bindings",
                columns: new[] { "ReannounceCount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_recurring_donations_cause",
                schema: "financial",
                table: "financial_recurring_donations",
                column: "CauseId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_recurring_donations_donor",
                schema: "financial",
                table: "financial_recurring_donations",
                columns: new[] { "DonorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_recurring_donations_due",
                schema: "financial",
                table: "financial_recurring_donations",
                columns: new[] { "Status", "NextChargeAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_recurring_donations_idempotency",
                schema: "financial",
                table: "financial_recurring_donations",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_campaign_fund_usage",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_campaign_milestones",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_campaign_updates",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_membership_payment_bindings",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_recurring_donations",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_campaigns",
                schema: "financial");
        }
    }
}

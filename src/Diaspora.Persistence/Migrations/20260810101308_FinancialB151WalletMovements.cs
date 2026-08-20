using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialB151WalletMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryAnnouncedFor",
                schema: "financial",
                table: "financial_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingChangeAt",
                schema: "financial",
                table: "financial_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPlanCode",
                schema: "financial",
                table: "financial_subscriptions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalDays",
                schema: "financial",
                table: "financial_purchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewEnabled",
                schema: "financial",
                table: "financial_placements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryAnnouncedFor",
                schema: "financial",
                table: "financial_placements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAt",
                schema: "financial",
                table: "financial_placements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PausedDuration",
                schema: "financial",
                table: "financial_placements",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "financial",
                table: "financial_payment_intents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "financial_payout_destinations",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderToken = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_payout_destinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_refund_requests",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OpenForPaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_refund_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_reward_accounts",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeEarned = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeConverted = table.Column<long>(type: "bigint", nullable: false),
                    IsConversionBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    BlockReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_reward_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_reward_conversions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    CreditedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    RateMinorUnitsPerPoint = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_reward_conversions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_webhook_events",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_withdrawals",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrossMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    FeeMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SettlementReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SettlementProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_withdrawals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_reward_entries",
                schema: "financial",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    IsConversion = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RewardAccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_reward_entries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_financial_reward_entries_financial_reward_accounts_RewardAc~",
                        column: x => x.RewardAccountId,
                        principalSchema: "financial",
                        principalTable: "financial_reward_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_payout_destinations_owner",
                schema: "financial",
                table: "financial_payout_destinations",
                columns: new[] { "OwnerUserId", "IsRemoved" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_refund_requests_open",
                schema: "financial",
                table: "financial_refund_requests",
                column: "OpenForPaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_refund_requests_requester",
                schema: "financial",
                table: "financial_refund_requests",
                columns: new[] { "RequestedByUserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_refund_requests_status",
                schema: "financial",
                table: "financial_refund_requests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_accounts_member",
                schema: "financial",
                table: "financial_reward_accounts",
                column: "MemberUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_conversions_idempotency",
                schema: "financial",
                table: "financial_reward_conversions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_conversions_member",
                schema: "financial",
                table: "financial_reward_conversions",
                columns: new[] { "MemberUserId", "ConvertedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_entries_reference",
                schema: "financial",
                table: "financial_reward_entries",
                columns: new[] { "RewardAccountId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_webhook_events_provider_event",
                schema: "financial",
                table: "financial_webhook_events",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_webhook_events_received",
                schema: "financial",
                table: "financial_webhook_events",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_withdrawals_idempotency",
                schema: "financial",
                table: "financial_withdrawals",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_withdrawals_requester",
                schema: "financial",
                table: "financial_withdrawals",
                columns: new[] { "RequestedByUserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_withdrawals_status",
                schema: "financial",
                table: "financial_withdrawals",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_payout_destinations",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_refund_requests",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_reward_conversions",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_reward_entries",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_webhook_events",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_withdrawals",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_reward_accounts",
                schema: "financial");

            migrationBuilder.DropColumn(
                name: "ExpiryAnnouncedFor",
                schema: "financial",
                table: "financial_subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingChangeAt",
                schema: "financial",
                table: "financial_subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingPlanCode",
                schema: "financial",
                table: "financial_subscriptions");

            migrationBuilder.DropColumn(
                name: "RenewalDays",
                schema: "financial",
                table: "financial_purchases");

            migrationBuilder.DropColumn(
                name: "AutoRenewEnabled",
                schema: "financial",
                table: "financial_placements");

            migrationBuilder.DropColumn(
                name: "ExpiryAnnouncedFor",
                schema: "financial",
                table: "financial_placements");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                schema: "financial",
                table: "financial_placements");

            migrationBuilder.DropColumn(
                name: "PausedDuration",
                schema: "financial",
                table: "financial_placements");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "financial",
                table: "financial_payment_intents");
        }
    }
}

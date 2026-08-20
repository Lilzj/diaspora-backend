using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "outbox_dead_letters",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PartitionKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CausationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReplayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_dead_letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_delivery_attempts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageSequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMilliseconds = table.Column<double>(type: "double precision", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_delivery_attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "platform",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PartitionKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CausationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Sequence);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dead_letters_correlation",
                schema: "platform",
                table: "outbox_dead_letters",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dead_letters_dead_lettered_at",
                schema: "platform",
                table: "outbox_dead_letters",
                column: "DeadLetteredAt");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dead_letters_message_id",
                schema: "platform",
                table: "outbox_dead_letters",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dead_letters_module_event_type",
                schema: "platform",
                table: "outbox_dead_letters",
                columns: new[] { "Module", "EventType" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_delivery_attempts_message",
                schema: "platform",
                table: "outbox_delivery_attempts",
                columns: new[] { "MessageId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_delivery_attempts_started_at",
                schema: "platform",
                table: "outbox_delivery_attempts",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_claim",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "State", "NextAttemptAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_claim_id",
                schema: "platform",
                table: "outbox_messages",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_correlation",
                schema: "platform",
                table: "outbox_messages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_lease",
                schema: "platform",
                table: "outbox_messages",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_message_id",
                schema: "platform",
                table: "outbox_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_module",
                schema: "platform",
                table: "outbox_messages",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_partition",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "PartitionKey", "State" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_retention",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "State", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "TenantId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_dead_letters",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "outbox_delivery_attempts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "platform");
        }
    }
}

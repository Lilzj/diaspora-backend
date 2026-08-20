using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "financial");

            migrationBuilder.CreateTable(
                name: "financial_accounts",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PrimaryCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RestrictionReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsPlatformAccount = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_contribution_funds",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    AcceptedKinds = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RaisedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TargetMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    ContributorCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_contribution_funds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_contributions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    FundCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContributorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    PledgeQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PledgeUnit = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PledgeDescription = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReceiptReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_contributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_credit_grants",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    IsExpired = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_credit_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_currencies",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_donation_causes",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BeneficiaryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcceptedKinds = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    RaisedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TargetMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    SupporterCount = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    LocationLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContactWhatsapp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MediaReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_donation_causes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_donations",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    BeneficiaryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CauseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CauseCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReceiptReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurringIntervalMonths = table.Column<int>(type: "integer", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_donations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_ledger_entries",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Bucket = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    BalanceBeforeMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfterMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    AuditActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuditActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuditReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AuditCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditPolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_payment_intents",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MethodKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    GatewayProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GatewayExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GatewayExternalStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CapturedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    RefundedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    NextActionPayload = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_payment_intents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_placements",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PriorityWeight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PurchasedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_placements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_policies",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_products",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PriceMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    PriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PriceExponent = table.Column<int>(type: "integer", nullable: true),
                    PricingPolicyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    PlacementKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PriorityWeight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsRenewable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AvailableTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_promotion_redemptions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_promotion_redemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_promotions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DiscountKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DiscountFixedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    DiscountCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DiscountMaximumMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    MaxPerSubject = table.Column<int>(type: "integer", nullable: true),
                    RedeemedCount = table.Column<int>(type: "integer", nullable: false),
                    CountryCodes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ProductCodes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MinimumSpendMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_purchases",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ListMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    DiscountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    PaidMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_purchases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_revenue_records",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DimensionValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    GrossMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CommissionMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    FeeMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    RefundMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    NetMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_revenue_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_subscription_plans",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    TrialDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    AudienceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_subscription_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_subscriptions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                    GraceEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false),
                    LastPaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AuditActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuditActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuditReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AuditCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuditPolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedByTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_wallets",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WalletType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AvailableMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    PendingMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    ReservedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeCreditMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeDebitMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    EntryCount = table.Column<long>(type: "bigint", nullable: false),
                    LastEntryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_wallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_payment_attempts",
                schema: "financial",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_payment_attempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_financial_payment_attempts_financial_payment_intents_Paymen~",
                        column: x => x.PaymentIntentId,
                        principalSchema: "financial",
                        principalTable: "financial_payment_intents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_payment_refunds",
                schema: "financial",
                columns: table => new
                {
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_payment_refunds", x => x.RefundId);
                    table.ForeignKey(
                        name: "FK_financial_payment_refunds_financial_payment_intents_Payment~",
                        column: x => x.PaymentIntentId,
                        principalSchema: "financial",
                        principalTable: "financial_payment_intents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_policy_parameters",
                schema: "financial",
                columns: table => new
                {
                    ParameterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FinancialPolicyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_policy_parameters", x => x.ParameterId);
                    table.ForeignKey(
                        name: "FK_financial_policy_parameters_financial_policies_FinancialPol~",
                        column: x => x.FinancialPolicyId,
                        principalSchema: "financial",
                        principalTable: "financial_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_product_eligibility",
                schema: "financial",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MonetizationProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_product_eligibility", x => x.RuleId);
                    table.ForeignKey(
                        name: "FK_financial_product_eligibility_financial_products_Monetizati~",
                        column: x => x.MonetizationProductId,
                        principalSchema: "financial",
                        principalTable: "financial_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_plan_entitlements",
                schema: "financial",
                columns: table => new
                {
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_plan_entitlements", x => x.EntitlementId);
                    table.ForeignKey(
                        name: "FK_financial_plan_entitlements_financial_subscription_plans_Su~",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "financial",
                        principalTable: "financial_subscription_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_subscription_entitlements",
                schema: "financial",
                columns: table => new
                {
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_subscription_entitlements", x => x.EntitlementId);
                    table.ForeignKey(
                        name: "FK_financial_subscription_entitlements_financial_subscriptions~",
                        column: x => x.SubscriptionId,
                        principalSchema: "financial",
                        principalTable: "financial_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_accounts_owner",
                schema: "financial",
                table: "financial_accounts",
                columns: new[] { "OwnerId", "AccountType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_accounts_platform",
                schema: "financial",
                table: "financial_accounts",
                column: "IsPlatformAccount");

            migrationBuilder.CreateIndex(
                name: "ix_financial_funds_active",
                schema: "financial",
                table: "financial_contribution_funds",
                columns: new[] { "IsActive", "CountryCode", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_funds_code",
                schema: "financial",
                table: "financial_contribution_funds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_contributions_contributor",
                schema: "financial",
                table: "financial_contributions",
                columns: new[] { "ContributorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_contributions_fund",
                schema: "financial",
                table: "financial_contributions",
                columns: new[] { "FundId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_contributions_receipt",
                schema: "financial",
                table: "financial_contributions",
                column: "ReceiptReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_credit_grants_expiry",
                schema: "financial",
                table: "financial_credit_grants",
                columns: new[] { "IsRevoked", "IsExpired", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_credit_grants_wallet",
                schema: "financial",
                table: "financial_credit_grants",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_currencies_code",
                schema: "financial",
                table: "financial_currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_causes_active",
                schema: "financial",
                table: "financial_donation_causes",
                columns: new[] { "IsActive", "CountryCode" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_causes_code",
                schema: "financial",
                table: "financial_donation_causes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_donations_cause",
                schema: "financial",
                table: "financial_donations",
                columns: new[] { "CauseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_donations_donor",
                schema: "financial",
                table: "financial_donations",
                columns: new[] { "DonorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_donations_receipt",
                schema: "financial",
                table: "financial_donations",
                column: "ReceiptReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_ledger_occurred",
                schema: "financial",
                table: "financial_ledger_entries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_ledger_transaction",
                schema: "financial",
                table: "financial_ledger_entries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_ledger_wallet_sequence",
                schema: "financial",
                table: "financial_ledger_entries",
                columns: new[] { "WalletId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_payment_attempts_ordinal",
                schema: "financial",
                table: "financial_payment_attempts",
                columns: new[] { "PaymentIntentId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_payment_intents_expiry",
                schema: "financial",
                table: "financial_payment_intents",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_payment_intents_external",
                schema: "financial",
                table: "financial_payment_intents",
                columns: new[] { "GatewayProvider", "GatewayExternalReference" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_payment_intents_idempotency",
                schema: "financial",
                table: "financial_payment_intents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_payment_intents_payer",
                schema: "financial",
                table: "financial_payment_intents",
                columns: new[] { "PayerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_payment_refunds_PaymentIntentId",
                schema: "financial",
                table: "financial_payment_refunds",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_placements_ends",
                schema: "financial",
                table: "financial_placements",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_placements_purchaser",
                schema: "financial",
                table: "financial_placements",
                columns: new[] { "PurchasedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_placements_status",
                schema: "financial",
                table: "financial_placements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_financial_placements_subject",
                schema: "financial",
                table: "financial_placements",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_plan_entitlements_unique",
                schema: "financial",
                table: "financial_plan_entitlements",
                columns: new[] { "SubscriptionPlanId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_policies_active",
                schema: "financial",
                table: "financial_policies",
                columns: new[] { "PolicyKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_policies_version",
                schema: "financial",
                table: "financial_policies",
                columns: new[] { "PolicyKey", "Scope", "ScopeValue", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_policy_parameters_unique",
                schema: "financial",
                table: "financial_policy_parameters",
                columns: new[] { "FinancialPolicyId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_product_eligibility_unique",
                schema: "financial",
                table: "financial_product_eligibility",
                columns: new[] { "MonetizationProductId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_products_available",
                schema: "financial",
                table: "financial_products",
                columns: new[] { "Status", "SubjectType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_products_code",
                schema: "financial",
                table: "financial_products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_redemptions_promotion_user",
                schema: "financial",
                table: "financial_promotion_redemptions",
                columns: new[] { "PromotionId", "RedeemedByUserId" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_redemptions_user",
                schema: "financial",
                table: "financial_promotion_redemptions",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_promotions_code",
                schema: "financial",
                table: "financial_promotions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_promotions_status",
                schema: "financial",
                table: "financial_promotions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_financial_purchases_buyer",
                schema: "financial",
                table: "financial_purchases",
                columns: new[] { "BuyerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_purchases_payment",
                schema: "financial",
                table: "financial_purchases",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_purchases_product",
                schema: "financial",
                table: "financial_purchases",
                columns: new[] { "ProductCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_revenue_period",
                schema: "financial",
                table: "financial_revenue_records",
                columns: new[] { "PeriodStart", "Dimension", "DimensionValue", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_subscription_entitlements_unique",
                schema: "financial",
                table: "financial_subscription_entitlements",
                columns: new[] { "SubscriptionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_plans_active",
                schema: "financial",
                table: "financial_subscription_plans",
                columns: new[] { "IsActive", "AudienceType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_plans_code",
                schema: "financial",
                table: "financial_subscription_plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_subscriptions_period_end",
                schema: "financial",
                table: "financial_subscriptions",
                column: "PeriodEndsAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_subscriptions_status",
                schema: "financial",
                table: "financial_subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_financial_subscriptions_subscriber",
                schema: "financial",
                table: "financial_subscriptions",
                columns: new[] { "SubscriberUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_occurred",
                schema: "financial",
                table: "financial_transactions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_reference",
                schema: "financial",
                table: "financial_transactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_user",
                schema: "financial",
                table: "financial_transactions",
                columns: new[] { "InitiatedByUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_wallets_account_type_currency",
                schema: "financial",
                table: "financial_wallets",
                columns: new[] { "AccountId", "WalletType", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_wallets_owner",
                schema: "financial",
                table: "financial_wallets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_accounts",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_contribution_funds",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_contributions",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_credit_grants",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_currencies",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_donation_causes",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_donations",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_ledger_entries",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_payment_attempts",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_payment_refunds",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_placements",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_plan_entitlements",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_policy_parameters",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_product_eligibility",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_promotion_redemptions",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_promotions",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_purchases",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_revenue_records",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_subscription_entitlements",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_transactions",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_wallets",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_payment_intents",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_subscription_plans",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_policies",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_products",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_subscriptions",
                schema: "financial");
        }
    }
}

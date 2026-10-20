START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    CREATE TABLE financial.financial_invoices (
        "Id" uuid NOT NULL,
        "Number" character varying(64) NOT NULL,
        "Year" integer NOT NULL,
        "Sequence" bigint NOT NULL,
        "OwnerUserId" uuid NOT NULL,
        "PaymentIntentId" uuid NOT NULL,
        "SourceKind" character varying(32) NOT NULL,
        "SourceId" uuid,
        "Description" character varying(1024) NOT NULL,
        "ListMinorUnits" bigint NOT NULL,
        "DiscountMinorUnits" bigint NOT NULL,
        "TotalMinorUnits" bigint NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Exponent" integer NOT NULL,
        "PromotionCode" character varying(64),
        "SubjectType" character varying(32),
        "SubjectId" uuid,
        "PeriodStart" timestamp with time zone NOT NULL,
        "PeriodEnd" timestamp with time zone NOT NULL,
        "PaidAt" timestamp with time zone NOT NULL,
        "IssuedAt" timestamp with time zone NOT NULL,
        "Revision" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_financial_invoices" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    CREATE INDEX ix_financial_invoices_owner ON financial.financial_invoices ("OwnerUserId", "Year", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    CREATE UNIQUE INDEX ux_financial_invoices_number ON financial.financial_invoices ("Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    CREATE UNIQUE INDEX ux_financial_invoices_payment ON financial.financial_invoices ("PaymentIntentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    CREATE UNIQUE INDEX ux_financial_invoices_sequence ON financial.financial_invoices ("Year", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070804_FinancialInvoices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020070804_FinancialInvoices', '10.0.13');
    END IF;
END $EF$;
COMMIT;


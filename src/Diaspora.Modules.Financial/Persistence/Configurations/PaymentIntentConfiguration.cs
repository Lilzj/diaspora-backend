using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps payment intents, with their attempts and refunds owned.
/// <para>
/// <b>Both owned collections declare a key and mark it <c>ValueGeneratedNever()</c>.</b> Without the
/// declaration EF invents a store-generated shadow integer inside a composite primary key and SQLite
/// refuses the insert once a row exists; without <c>ValueGeneratedNever</c> EF emits UPDATE where
/// INSERT was meant, which surfaces as a bogus concurrency conflict on the second row. Both are on
/// the platform's record four times over and both are now analyzer rules — DSP3002 — so this is the
/// shape that compiles rather than a convention somebody remembered.
/// </para>
/// <para>
/// The unique index on the idempotency key is what refuses a double-tapped pay button at the
/// database, rather than at a handler check a refactor can step around.
/// </para>
/// </summary>
public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.PaymentIntentsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(intent => intent.Id);

        builder.Property(intent => intent.Id)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(intent => intent.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(intent => intent.Revision).IsConcurrencyToken();

        builder.Property(intent => intent.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(intent => intent.MethodKind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(intent => intent.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(intent => intent.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(intent => intent.Purpose)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(intent => intent.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength);

        builder.Property(intent => intent.GatewayProvider)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(intent => intent.GatewayExternalReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(intent => intent.GatewayExternalStatus)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(intent => intent.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(intent => intent.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(intent => intent.NextActionPayload)
            .HasMaxLength(FinancialPersistenceConstants.PayloadLength);

        builder.Property(intent => intent.FailureCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(intent => intent.FailureMessage)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.OwnsMany(intent => intent.Attempts, attempt =>
        {
            attempt.ToTable(
                FinancialPersistenceConstants.PaymentAttemptsTable, FinancialModuleMetadata.Schema);

            attempt.HasKey(record => record.AttemptId);
            attempt.Property(record => record.AttemptId).ValueGeneratedNever();

            attempt.Property(record => record.Status)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .IsRequired();

            attempt.Property(record => record.Provider)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength);

            attempt.Property(record => record.ExternalReference)
                .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

            attempt.Property(record => record.FailureCode)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength);

            attempt.Property(record => record.FailureMessage)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

            attempt.HasIndex("PaymentIntentId", nameof(Domain.Payments.PaymentAttempt.Ordinal))
                .IsUnique()
                .HasDatabaseName("ix_financial_payment_attempts_ordinal");
        });

        builder.OwnsMany(intent => intent.Refunds, refund =>
        {
            refund.ToTable(
                FinancialPersistenceConstants.RefundsTable, FinancialModuleMetadata.Schema);

            refund.HasKey(record => record.RefundId);
            refund.Property(record => record.RefundId).ValueGeneratedNever();

            refund.Property(record => record.Reason)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .IsRequired();

            refund.Property(record => record.Status)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .IsRequired();

            refund.Property(record => record.Note)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

            refund.Property(record => record.ExternalReference)
                .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);
        });

        builder.HasIndex(intent => intent.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_financial_payment_intents_idempotency");

        builder.HasIndex(intent => new { intent.GatewayProvider, intent.GatewayExternalReference })
            .HasDatabaseName("ix_financial_payment_intents_external");

        builder.HasIndex(intent => new { intent.PayerUserId, intent.CreatedAt })
            .HasDatabaseName("ix_financial_payment_intents_payer");

        builder.HasIndex(intent => new { intent.Status, intent.ExpiresAt })
            .HasDatabaseName("ix_financial_payment_intents_expiry");

        builder.Ignore(intent => intent.DomainEvents);
        builder.Ignore(intent => intent.Amount);
        builder.Ignore(intent => intent.Captured);
        builder.Ignore(intent => intent.Refunded);
        builder.Ignore(intent => intent.RefundableRemaining);
        builder.Ignore(intent => intent.Subject);
        builder.Ignore(intent => intent.Gateway);
        builder.Ignore(intent => intent.IsTerminal);
    }
}

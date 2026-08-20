using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps transactions.
/// <para>
/// <b>The unique index on the reference is the idempotency guarantee.</b> A replayed request, a
/// double-tapped button and a redelivered webhook all arrive carrying one key, and the database is
/// what refuses the second — rather than a handler check a future refactor can step around.
/// </para>
/// </summary>
public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.TransactionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(transaction => transaction.Revision).IsConcurrencyToken();

        builder.Property(transaction => transaction.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(transaction => transaction.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(transaction => transaction.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(transaction => transaction.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength);

        builder.Property(transaction => transaction.Reference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(transaction => transaction.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(transaction => transaction.FailureReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.Property(transaction => transaction.ReversalOfTransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(transaction => transaction.ReversedByTransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.OwnsOne(transaction => transaction.Audit, audit =>
        {
            audit.Property(stamp => stamp.Source)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .HasColumnName("AuditSource")
                .IsRequired();

            audit.Property(stamp => stamp.Reason)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength)
                .HasColumnName("AuditReason")
                .IsRequired();

            audit.Property(stamp => stamp.CorrelationId)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength)
                .HasColumnName("AuditCorrelationId");

            audit.Property(stamp => stamp.RequestId)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength)
                .HasColumnName("AuditRequestId");

            audit.Property(stamp => stamp.PolicyVersion)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength)
                .HasColumnName("AuditPolicyVersion");

            audit.OwnsOne(stamp => stamp.Actor, actor =>
            {
                actor.Property(who => who.ActorType)
                    .HasConversion<string>()
                    .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                    .HasColumnName("AuditActorType")
                    .IsRequired();

                actor.Property(who => who.UserId).HasColumnName("AuditActorUserId");

                actor.Property(who => who.Label)
                    .HasMaxLength(FinancialPersistenceConstants.LabelLength)
                    .HasColumnName("AuditActorLabel");
            });

            audit.Navigation(stamp => stamp.Actor).IsRequired();
        });

        builder.Navigation(transaction => transaction.Audit).IsRequired();

        builder.HasIndex(transaction => transaction.Reference)
            .IsUnique()
            .HasDatabaseName("ix_financial_transactions_reference");

        builder.HasIndex(transaction => new { transaction.InitiatedByUserId, transaction.OccurredAt })
            .HasDatabaseName("ix_financial_transactions_user");

        builder.HasIndex(transaction => transaction.OccurredAt)
            .HasDatabaseName("ix_financial_transactions_occurred");

        builder.Ignore(transaction => transaction.DomainEvents);
        builder.Ignore(transaction => transaction.Amount);
        builder.Ignore(transaction => transaction.Subject);
    }
}

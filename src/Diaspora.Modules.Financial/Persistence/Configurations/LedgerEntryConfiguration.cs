using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the ledger.
/// <para>
/// <b>The largest table on the platform, and the one nothing may ever update.</b> There is no
/// concurrency token because there is no update path; there is no soft-delete filter because there
/// is no delete path. A correction is an appended compensating entry with its own audit stamp.
/// </para>
/// <para>
/// <b>The unique index on wallet and sequence is the gapless-statement guarantee.</b> Two entries
/// claiming one position would make a cursor-paged statement skip or repeat a line, which is the
/// kind of defect only ever found by a customer.
/// </para>
/// <para>
/// The audit stamp is a required owned type with the actor owned inside it. Required rather than
/// optional throughout, so none of EF's optional-dependent ambiguity applies.
/// </para>
/// </summary>
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.LedgerEntriesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasConversion(FinancialValueConverters.Id<LedgerEntryId>());

        builder.Property(entry => entry.WalletId)
            .HasConversion(FinancialValueConverters.Id<WalletId>())
            .IsRequired();

        builder.Property(entry => entry.TransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>())
            .IsRequired();

        builder.Property(entry => entry.EntryType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entry => entry.Direction)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entry => entry.Bucket)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(entry => entry.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength);

        builder.Property(entry => entry.ExternalReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.OwnsOne(entry => entry.Audit, audit =>
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

        builder.Navigation(entry => entry.Audit).IsRequired();

        builder.HasIndex(entry => new { entry.WalletId, entry.Sequence })
            .IsUnique()
            .HasDatabaseName("ix_financial_ledger_wallet_sequence");

        builder.HasIndex(entry => entry.TransactionId)
            .HasDatabaseName("ix_financial_ledger_transaction");

        builder.HasIndex(entry => entry.OccurredAt)
            .HasDatabaseName("ix_financial_ledger_occurred");

        builder.Ignore(entry => entry.DomainEvents);
        builder.Ignore(entry => entry.Amount);
        builder.Ignore(entry => entry.Subject);
        builder.Ignore(entry => entry.SignedMinorUnits);
    }
}

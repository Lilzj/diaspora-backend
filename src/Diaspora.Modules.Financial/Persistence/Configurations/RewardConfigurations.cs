using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps a member's points balance and the entries behind it.
/// <para>
/// <b>No currency column, anywhere.</b> A point is a count, and a schema that gave it a currency
/// would be a schema that let somebody sum it with money.
/// </para>
/// <para>
/// The entries are an owned collection with a declared, caller-supplied key — the shape DSP3002
/// enforces, and the one that stops EF inventing a shadow integer inside a composite primary key
/// that SQLite then refuses.
/// </para>
/// </summary>
public sealed class RewardAccountConfiguration : IEntityTypeConfiguration<RewardAccount>
{
    public void Configure(EntityTypeBuilder<RewardAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.RewardAccountsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasConversion(FinancialValueConverters.Id<RewardAccountId>());

        builder.Property(account => account.Revision).IsConcurrencyToken();

        builder.Property(account => account.BlockReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.OwnsMany(account => account.Entries, entry =>
        {
            entry.ToTable(
                FinancialPersistenceConstants.RewardEntriesTable, FinancialModuleMetadata.Schema);

            entry.HasKey(record => record.EntryId);
            entry.Property(record => record.EntryId).ValueGeneratedNever();

            entry.Property(record => record.Source)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength)
                .IsRequired();

            entry.Property(record => record.Reference)
                .HasMaxLength(FinancialPersistenceConstants.ReferenceLength)
                .IsRequired();

            entry.Property(record => record.Reason)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

            entry.Property(record => record.PolicyVersion)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength);

            entry.Property(record => record.ReversalReason)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

            // What the vesting sweep reads (D-9): pending grants that are due.
            entry.HasIndex(nameof(RewardEntry.VestedAt), nameof(RewardEntry.VestsAt))
                .HasDatabaseName("ix_financial_reward_entries_vesting");

            // What the reward.grant policy's daily ceiling sums: one staff member's grants since a
            // moment, without scanning every member's history.
            entry.HasIndex(nameof(RewardEntry.GrantedByUserId), nameof(RewardEntry.OccurredAt))
                .HasDatabaseName("ix_financial_reward_entries_grantor");

            // One grant reference per account, refused at the database. Two deliveries of one
            // integration event award points once even when they race each other.
            entry.HasIndex("RewardAccountId", nameof(RewardEntry.Reference))
                .IsUnique()
                .HasDatabaseName("ix_financial_reward_entries_reference");
        });

        builder.OwnsMany(account => account.HoldDecisions, decision =>
        {
            decision.ToTable(
                FinancialPersistenceConstants.RewardHoldDecisionsTable, FinancialModuleMetadata.Schema);

            decision.HasKey(record => record.DecisionId);
            decision.Property(record => record.DecisionId).ValueGeneratedNever();

            decision.Property(record => record.Reason)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength)
                .IsRequired();
        });

        builder.HasIndex(account => account.MemberUserId)
            .IsUnique()
            .HasDatabaseName("ix_financial_reward_accounts_member");

        builder.Ignore(account => account.DomainEvents);
    }
}

/// <summary>Maps the record of every crossing from points into money.</summary>
public sealed class RewardConversionConfiguration : IEntityTypeConfiguration<RewardConversion>
{
    public void Configure(EntityTypeBuilder<RewardConversion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.RewardConversionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(conversion => conversion.Id);

        builder.Property(conversion => conversion.Id)
            .HasConversion(FinancialValueConverters.Id<RewardConversionId>());

        builder.Property(conversion => conversion.WalletId)
            .HasConversion(FinancialValueConverters.Id<WalletId>())
            .IsRequired();

        builder.Property(conversion => conversion.TransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(conversion => conversion.Revision).IsConcurrencyToken();

        builder.Property(conversion => conversion.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        // The applied rate, stored to six decimals. A double would lose it silently, which on a
        // figure that decides how much money somebody gets is the wrong kind of silent.
        builder.Property(conversion => conversion.RateMinorUnitsPerPoint)
            .HasPrecision(
                FinancialPersistenceConstants.RatePrecision,
                FinancialPersistenceConstants.RateScale);

        builder.Property(conversion => conversion.PolicyVersion)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(conversion => conversion.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        // Unique per member, as the replay lookup always was. It used to be unique across everyone,
        // so a second member who happened to choose the same key found no replay of their own and
        // then hit the index: a 500 for an ordinary request.
        builder.HasIndex(conversion => new { conversion.MemberUserId, conversion.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ix_financial_reward_conversions_member_idempotency");

        // What the release sweep reads: holds that have not been released, by when they end.
        builder.HasIndex(conversion => new { conversion.ReleasedAt, conversion.AvailableAt })
            .HasDatabaseName("ix_financial_reward_conversions_release");

        builder.HasIndex(conversion => new { conversion.MemberUserId, conversion.ConvertedAt })
            .HasDatabaseName("ix_financial_reward_conversions_member");

        builder.Ignore(conversion => conversion.DomainEvents);
        builder.Ignore(conversion => conversion.Credited);
    }
}

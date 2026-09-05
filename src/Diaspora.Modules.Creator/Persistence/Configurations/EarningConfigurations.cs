using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>
/// One recorded earning.
/// <para>
/// <b>Money as three columns, not one.</b> `AmountMinorUnits`, `Currency` and `Exponent` are stored
/// separately, matching every other money-bearing aggregate on the platform. A composite value
/// object would round-trip through a converter and lose the ability to sum at the database, which is
/// exactly what the revenue summary needs to do.
/// </para>
/// <para>
/// <b>The indexes are the summary's shape.</b> A revenue dashboard asks three questions — the whole
/// ledger newest-first, the total per state, and the total per source — and each has an index here.
/// Without them the summary is a full scan that gets slower every month a creator earns anything.
/// </para>
/// </summary>
public sealed class CreatorEarningConfiguration : IEntityTypeConfiguration<CreatorEarning>
{
    public void Configure(EntityTypeBuilder<CreatorEarning> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.EarningsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(earning => earning.Id);

        builder.Property(earning => earning.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorEarningId>())
            .ValueGeneratedNever();

        builder.Property(earning => earning.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(earning => earning.Currency)
            .HasMaxLength(CreatorPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(earning => earning.SourceReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength)
            .IsRequired();

        builder.Property(earning => earning.SubjectType)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(earning => earning.StateReason)
            .HasMaxLength(CreatorPersistenceConstants.DescriptionLength);

        // The clearing sweep and a payout can both touch one earning, and the loser of that race
        // must be told rather than silently overwrite the winner. Only IsConcurrencyToken puts
        // Revision in the WHERE clause.
        builder.Property(earning => earning.Revision)
            .IsConcurrencyToken();

        // At-least-once delivery, paid once.
        //
        // Unique on the reference **alone**, not on (creator, reference) as the activity ledger is.
        // An activity entry is scoped to a creator by nature; an earning's reference already names
        // one payment, and the same payment must not produce an earning for two creators through a
        // consumer bug. The narrower index is the stronger statement.
        builder.HasIndex(earning => earning.SourceReference)
            .IsUnique()
            .HasDatabaseName("ux_creator_earning_source");

        // The ledger, newest first.
        builder.HasIndex(earning => new { earning.CreatorId, earning.OccurredAt })
            .HasDatabaseName("ix_creator_earning_ledger");

        // "How much is available", which is the only figure a creator reads twice.
        builder.HasIndex(earning => new { earning.CreatorId, earning.State })
            .HasDatabaseName("ix_creator_earning_state");

        // The per-source breakdown the register asks for.
        builder.HasIndex(earning => new { earning.CreatorId, earning.Source })
            .HasDatabaseName("ix_creator_earning_source_total");

        // The clearing sweep: pending earnings whose waiting period has elapsed. Filtered on state
        // first because the overwhelming majority of rows are not pending.
        builder.HasIndex(earning => new { earning.State, earning.OccurredAt })
            .HasDatabaseName("ix_creator_earning_clearing");

        builder.Ignore(earning => earning.DomainEvents);
    }
}

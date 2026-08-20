using Diaspora.Platform.Messaging.Outbox.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Outbox.Configurations;

/// <summary>
/// Maps the delivery table.
/// <para>
/// <b>The indexes are the design.</b> This table is read several times a second by every
/// instance, and the claim query is the single hottest statement the platform runs. Everything
/// else here — column widths, the string-valued state, the unique message identifier — protects
/// a durability guarantee; the indexes decide whether the guarantee is affordable.
/// </para>
/// <para>
/// No provider-specific types appear. Production is PostgreSQL and the integration suite is
/// SQLite, and a <c>jsonb</c> payload column would mean the suite exercised a different schema
/// from the one that ships — which for a component whose whole purpose is durability would make
/// the tests worth very little.
/// </para>
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(OutboxSchema.Messages, OutboxSchema.Name);

        // Sequence is the key, and it is database-generated. It is the only ordering source
        // that survives clock skew between instances, which is what makes the ordering
        // guarantee a guarantee rather than a hope about NTP.
        builder.HasKey(message => message.Sequence);

        builder.Property(message => message.Sequence)
            .ValueGeneratedOnAdd();

        builder.Property(message => message.MessageId)
            .IsRequired();

        builder.Property(message => message.EventType)
            .HasMaxLength(OutboxFieldLimits.EventType)
            .IsRequired();

        builder.Property(message => message.EventVersion)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.Metadata);

        builder.Property(message => message.Module)
            .HasMaxLength(OutboxFieldLimits.Module)
            .IsRequired();

        builder.Property(message => message.PartitionKey)
            .HasMaxLength(OutboxFieldLimits.PartitionKey)
            .IsRequired();

        builder.Property(message => message.TenantId)
            .HasMaxLength(OutboxFieldLimits.TenantId);

        builder.Property(message => message.CorrelationId)
            .HasMaxLength(OutboxFieldLimits.CorrelationId);

        builder.Property(message => message.CausationId)
            .HasMaxLength(OutboxFieldLimits.CausationId);

        // Stored as text. An ordinal binds every row already in the table to the current source
        // order of the enum, and reordering an enum is a change nobody reviews as breaking.
        builder.Property(message => message.State)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasMaxLength(OutboxFieldLimits.Error);

        builder.Property(message => message.LeaseOwner)
            .HasMaxLength(OutboxFieldLimits.WorkerId);

        // The idempotency key, and the guard against a double enqueue of the same event. A
        // producer that somehow published twice inside one transaction fails the insert rather
        // than committing two rows a consumer would have to reconcile.
        builder.HasIndex(message => message.MessageId)
            .IsUnique()
            .HasDatabaseName("ix_outbox_messages_message_id");

        // The claim query, in the order it filters: state first (the most selective by far once
        // retention is running), then eligibility, then sequence for ordering. A composite in
        // this order lets the database answer the whole predicate and the ORDER BY from one
        // index, which is the difference between a seek and a scan of the entire backlog.
        builder.HasIndex(message => new
            {
                message.State,
                message.NextAttemptAt,
                message.Sequence
            })
            .HasDatabaseName("ix_outbox_messages_claim");

        // Partition blocking: "does this partition already have something in flight or waiting".
        // Without it, every claim under partitioned ordering scans the pending population once
        // per pass to answer a question about a handful of rows.
        builder.HasIndex(message => new { message.PartitionKey, message.State })
            .HasDatabaseName("ix_outbox_messages_partition");

        // Lease recovery, run on a timer by every instance.
        builder.HasIndex(message => message.LeaseExpiresAt)
            .HasDatabaseName("ix_outbox_messages_lease");

        // Reading back the rows a claim just won.
        builder.HasIndex(message => message.ClaimId)
            .HasDatabaseName("ix_outbox_messages_claim_id");

        // The retention sweep, which would otherwise scan the table it exists to shrink.
        builder.HasIndex(message => new { message.State, message.DeliveredAt })
            .HasDatabaseName("ix_outbox_messages_retention");

        // Per-module and per-tenant statistics, and the tenant-filtered claim.
        builder.HasIndex(message => new { message.TenantId, message.State })
            .HasDatabaseName("ix_outbox_messages_tenant");

        builder.HasIndex(message => message.Module)
            .HasDatabaseName("ix_outbox_messages_module");

        // Following one request's trail from an HTTP log into the messages it produced.
        builder.HasIndex(message => message.CorrelationId)
            .HasDatabaseName("ix_outbox_messages_correlation");
    }
}

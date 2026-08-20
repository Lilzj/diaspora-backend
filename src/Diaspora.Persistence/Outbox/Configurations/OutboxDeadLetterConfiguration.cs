using Diaspora.Platform.Messaging.Outbox.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Outbox.Configurations;

/// <summary>
/// Maps the dead-letter table.
/// <para>
/// The cold half of the outbox. It is written rarely, read during incidents and kept far longer
/// than the delivery table, which is exactly why it is a separate table rather than a flag: the
/// hot path never pays for the population it must not forget.
/// </para>
/// <para>
/// <c>MessageId</c> is indexed but <b>not unique</b>. A dead letter that is replayed and dies
/// again produces a second row under the same identifier, and that sequence is the history an
/// operator needs — collapsing it would answer "did this fail before?" with silence.
/// </para>
/// </summary>
public sealed class OutboxDeadLetterConfiguration : IEntityTypeConfiguration<OutboxDeadLetter>
{
    public void Configure(EntityTypeBuilder<OutboxDeadLetter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(OutboxSchema.DeadLetters, OutboxSchema.Name);

        builder.HasKey(deadLetter => deadLetter.Id);

        builder.Property(deadLetter => deadLetter.Id)
            .ValueGeneratedOnAdd();

        builder.Property(deadLetter => deadLetter.MessageId)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.EventType)
            .HasMaxLength(OutboxFieldLimits.EventType)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.Payload)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.Metadata);

        builder.Property(deadLetter => deadLetter.Module)
            .HasMaxLength(OutboxFieldLimits.Module)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.PartitionKey)
            .HasMaxLength(OutboxFieldLimits.PartitionKey)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.TenantId)
            .HasMaxLength(OutboxFieldLimits.TenantId);

        builder.Property(deadLetter => deadLetter.CorrelationId)
            .HasMaxLength(OutboxFieldLimits.CorrelationId);

        builder.Property(deadLetter => deadLetter.CausationId)
            .HasMaxLength(OutboxFieldLimits.CausationId);

        builder.Property(deadLetter => deadLetter.Reason)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(deadLetter => deadLetter.Error)
            .HasMaxLength(OutboxFieldLimits.Error)
            .IsRequired();

        builder.HasIndex(deadLetter => deadLetter.MessageId)
            .HasDatabaseName("ix_outbox_dead_letters_message_id");

        // The default listing: most recent first, and the retention sweep's cutoff.
        builder.HasIndex(deadLetter => deadLetter.DeadLetteredAt)
            .HasDatabaseName("ix_outbox_dead_letters_dead_lettered_at");

        // "Replay everything from Community that failed after the deploy" — the bulk-replay
        // filter, and the first thing anyone reaches for once a dependency is back.
        builder.HasIndex(deadLetter => new { deadLetter.Module, deadLetter.EventType })
            .HasDatabaseName("ix_outbox_dead_letters_module_event_type");

        builder.HasIndex(deadLetter => deadLetter.CorrelationId)
            .HasDatabaseName("ix_outbox_dead_letters_correlation");
    }
}

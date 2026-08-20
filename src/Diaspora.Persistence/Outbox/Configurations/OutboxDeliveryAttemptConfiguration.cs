using Diaspora.Platform.Messaging.Outbox.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Outbox.Configurations;

/// <summary>
/// Maps the optional attempt log.
/// <para>
/// The table exists in every deployment; whether anything writes to it is a runtime switch. An
/// empty table costs nothing, and creating it only when enabled would mean turning the switch
/// on required a migration — during the incident it was needed for.
/// </para>
/// <para>
/// Deliberately not a foreign key to <c>outbox_messages</c>. The attempt log outlives the
/// message it describes: retention removes delivered messages after days and attempts after a
/// week, and a message that dead-letters is deleted from the delivery table outright. A
/// referential constraint would force those lifetimes to match, which is the opposite of what
/// either table wants.
/// </para>
/// </summary>
public sealed class OutboxDeliveryAttemptConfiguration : IEntityTypeConfiguration<OutboxDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<OutboxDeliveryAttempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(OutboxSchema.DeliveryAttempts, OutboxSchema.Name);

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id)
            .ValueGeneratedOnAdd();

        builder.Property(attempt => attempt.MessageId)
            .IsRequired();

        builder.Property(attempt => attempt.EventType)
            .HasMaxLength(OutboxFieldLimits.EventType)
            .IsRequired();

        builder.Property(attempt => attempt.Error)
            .HasMaxLength(OutboxFieldLimits.Error);

        builder.Property(attempt => attempt.WorkerId)
            .HasMaxLength(OutboxFieldLimits.WorkerId)
            .IsRequired();

        // "Show me every attempt at this message, in order" — the reason the table exists.
        builder.HasIndex(attempt => new { attempt.MessageId, attempt.AttemptNumber })
            .HasDatabaseName("ix_outbox_delivery_attempts_message");

        // The retention sweep. This table grows faster than any other in the platform when
        // recording is on, so its cutoff must be a seek.
        builder.HasIndex(attempt => attempt.StartedAt)
            .HasDatabaseName("ix_outbox_delivery_attempts_started_at");
    }
}

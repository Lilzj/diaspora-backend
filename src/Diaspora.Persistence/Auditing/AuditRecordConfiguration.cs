using Diaspora.Platform.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Auditing;

/// <summary>Table and schema names for the audit trail: platform infrastructure, like the outbox.</summary>
public static class AuditSchema
{
    public const string Name = "platform";

    public const string Records = "audit_records";
}

/// <summary>
/// Maps the stored audit trail.
/// <para>
/// The three indexes are the three questions the trail is read for: what happened to this thing,
/// what did this person do, and what happened in this window.
/// </para>
/// </summary>
public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AuditSchema.Records, AuditSchema.Name);

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.OccurredAt).IsRequired();
        builder.Property(record => record.ActorUserId).HasMaxLength(AuditRecord.ActorLength);
        builder.Property(record => record.ActorRole).HasMaxLength(AuditRecord.RoleLength);
        builder.Property(record => record.Action).HasMaxLength(AuditRecord.ActionLength).IsRequired();
        builder.Property(record => record.EntityType).HasMaxLength(AuditRecord.EntityTypeLength).IsRequired();
        builder.Property(record => record.EntityId).HasMaxLength(AuditRecord.EntityIdLength);
        builder.Property(record => record.Reason).HasMaxLength(AuditRecord.ReasonLength);
        builder.Property(record => record.Detail).HasMaxLength(AuditRecord.DetailLength);
        builder.Property(record => record.CorrelationId).HasMaxLength(AuditRecord.CorrelationLength);

        builder.HasIndex(record => new { record.EntityId, record.OccurredAt })
            .HasDatabaseName("ix_audit_records_entity");

        builder.HasIndex(record => new { record.ActorUserId, record.OccurredAt })
            .HasDatabaseName("ix_audit_records_actor");

        builder.HasIndex(record => record.OccurredAt)
            .HasDatabaseName("ix_audit_records_occurred_at");
    }
}

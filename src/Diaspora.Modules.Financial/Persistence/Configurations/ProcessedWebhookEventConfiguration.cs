using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the record of received provider callbacks.
/// <para>
/// <b>The unique index is the whole point of the table.</b> Replay protection enforced by the
/// database rather than by a handler check, so two workers processing the same redelivery race into
/// a constraint violation instead of into two captures.
/// </para>
/// </summary>
public sealed class ProcessedWebhookEventConfiguration
    : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.WebhookEventsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasConversion(FinancialValueConverters.Id<WebhookEventId>());

        builder.Property(record => record.Provider)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(record => record.ProviderEventId)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength)
            .IsRequired();

        builder.Property(record => record.Outcome)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(record => record.EventKind)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(record => record.ExternalReference)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(record => record.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(record => record.Detail)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(record => new { record.Provider, record.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("ix_financial_webhook_events_provider_event");

        builder.HasIndex(record => record.ReceivedAt)
            .HasDatabaseName("ix_financial_webhook_events_received");

        builder.Ignore(record => record.DomainEvents);
    }
}

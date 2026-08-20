using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps member refund requests.
/// <para>
/// <b>The unique index is filtered on <c>IsOpen</c>, which is why that property is stored rather than
/// derived.</b> One open request per payment, enforced at the database; a rejected one leaves the
/// payment free to be asked about again, which matters because the commonest rejection is "send us
/// the evidence".
/// </para>
/// </summary>
public sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.RefundRequestsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(FinancialValueConverters.Id<RefundRequestId>());

        builder.Property(request => request.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>())
            .IsRequired();

        builder.Property(request => request.Revision).IsConcurrencyToken();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(request => request.Reason)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(request => request.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(request => request.Note)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength)
            .IsRequired();

        builder.Property(request => request.DecisionNote)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        // One open request per payment. Nulls are distinct in a unique index on both PostgreSQL and
        // SQLite, so an answered request stops competing without a provider-specific filter.
        builder.HasIndex(request => request.OpenForPaymentIntentId)
            .IsUnique()
            .HasDatabaseName("ix_financial_refund_requests_open");

        builder.HasIndex(request => new { request.RequestedByUserId, request.RequestedAt })
            .HasDatabaseName("ix_financial_refund_requests_requester");

        builder.HasIndex(request => new { request.Status, request.RequestedAt })
            .HasDatabaseName("ix_financial_refund_requests_status");

        builder.Ignore(request => request.DomainEvents);
        builder.Ignore(request => request.Amount);
        builder.Ignore(request => request.IsOpen);
    }
}

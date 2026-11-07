using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps credit grants waiting for a second Admin (7 Nov 2026).</summary>
public sealed class CreditGrantRequestConfiguration : IEntityTypeConfiguration<CreditGrantRequest>
{
    public void Configure(EntityTypeBuilder<CreditGrantRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.CreditGrantRequestsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(FinancialValueConverters.Id<CreditGrantRequestId>());

        builder.Property(request => request.CreditGrantId)
            .HasConversion(FinancialValueConverters.Id<CreditGrantId>());

        builder.Property(request => request.Revision).IsConcurrencyToken();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(request => request.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(request => request.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(request => request.Reason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength)
            .IsRequired();

        builder.Property(request => request.DecisionNote)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.HasIndex(request => new { request.Status, request.RequestedAt })
            .HasDatabaseName("ix_financial_credit_grant_requests_status");

        builder.Ignore(request => request.DomainEvents);
        builder.Ignore(request => request.IsPending);
        builder.Ignore(request => request.Amount);
    }
}

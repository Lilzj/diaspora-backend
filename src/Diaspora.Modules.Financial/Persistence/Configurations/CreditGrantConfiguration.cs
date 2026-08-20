using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps credit grants.</summary>
public sealed class CreditGrantConfiguration : IEntityTypeConfiguration<CreditGrant>
{
    public void Configure(EntityTypeBuilder<CreditGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.CreditGrantsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.Id)
            .HasConversion(FinancialValueConverters.Id<CreditGrantId>());

        builder.Property(grant => grant.WalletId)
            .HasConversion(FinancialValueConverters.Id<WalletId>())
            .IsRequired();

        builder.Property(grant => grant.TransactionId)
            .HasConversion(FinancialValueConverters.Id<TransactionId>());

        builder.Property(grant => grant.PromotionId)
            .HasConversion(FinancialValueConverters.Id<PromotionId>());

        builder.Property(grant => grant.Revision).IsConcurrencyToken();

        builder.Property(grant => grant.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(grant => grant.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(grant => grant.Reason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength)
            .IsRequired();

        builder.HasIndex(grant => grant.WalletId)
            .HasDatabaseName("ix_financial_credit_grants_wallet");

        builder.HasIndex(grant => new { grant.IsRevoked, grant.IsExpired, grant.ExpiresAt })
            .HasDatabaseName("ix_financial_credit_grants_expiry");

        builder.Ignore(grant => grant.DomainEvents);
        builder.Ignore(grant => grant.Amount);
        builder.Ignore(grant => grant.IsLive);
    }
}

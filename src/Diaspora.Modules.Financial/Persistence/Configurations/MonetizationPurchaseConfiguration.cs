using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps purchases.
/// <para>
/// The subject is a required owned type: every purchase is about something, even a subscription,
/// which is about the buyer. Required rather than optional, so none of EF's optional-dependent
/// ambiguity applies.
/// </para>
/// </summary>
public sealed class MonetizationPurchaseConfiguration
    : IEntityTypeConfiguration<MonetizationPurchase>
{
    public void Configure(EntityTypeBuilder<MonetizationPurchase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.PurchasesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(purchase => purchase.Id);

        builder.Property(purchase => purchase.Id)
            .HasConversion(FinancialValueConverters.Id<MonetizationPurchaseId>());

        builder.Property(purchase => purchase.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(purchase => purchase.ProductId)
            .HasConversion(FinancialValueConverters.Id<MonetizationProductId>())
            .IsRequired();

        builder.Property(purchase => purchase.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(purchase => purchase.PromotionId)
            .HasConversion(FinancialValueConverters.Id<PromotionId>());

        builder.Property(purchase => purchase.PlacementId)
            .HasConversion(FinancialValueConverters.Id<PlacementId>());

        builder.Property(purchase => purchase.Revision).IsConcurrencyToken();

        builder.Property(purchase => purchase.ProductCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(purchase => purchase.Category)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(purchase => purchase.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(purchase => purchase.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(purchase => purchase.PromotionCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(purchase => purchase.PolicyVersion)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(purchase => purchase.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(purchase => purchase.FailureReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.OwnsOne(purchase => purchase.Subject, subject =>
        {
            subject.Property(reference => reference.SubjectType)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .HasColumnName("SubjectType")
                .IsRequired();

            subject.Property(reference => reference.SubjectId)
                .HasColumnName("SubjectId")
                .IsRequired();
        });

        builder.Navigation(purchase => purchase.Subject).IsRequired();

        builder.HasIndex(purchase => new { purchase.BuyerUserId, purchase.CreatedAt })
            .HasDatabaseName("ix_financial_purchases_buyer");

        builder.HasIndex(purchase => purchase.PaymentIntentId)
            .HasDatabaseName("ix_financial_purchases_payment");

        builder.HasIndex(purchase => new { purchase.ProductCode, purchase.Status })
            .HasDatabaseName("ix_financial_purchases_product");

        builder.Ignore(purchase => purchase.DomainEvents);
        builder.Ignore(purchase => purchase.ListPrice);
        builder.Ignore(purchase => purchase.Paid);
        builder.Ignore(purchase => purchase.Discount);
    }
}

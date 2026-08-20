using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps redemptions.
/// <para>
/// Its own table rather than a collection on the promotion: a campaign discount used by forty
/// thousand people would make the promotion aggregate unloadable, and the question asked at
/// redemption time is an indexed lookup rather than a scan.
/// </para>
/// </summary>
public sealed class PromotionRedemptionConfiguration
    : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.RedemptionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(redemption => redemption.Id);

        builder.Property(redemption => redemption.Id)
            .HasConversion(FinancialValueConverters.Id<PromotionRedemptionId>());

        builder.Property(redemption => redemption.PromotionId)
            .HasConversion(FinancialValueConverters.Id<PromotionId>())
            .IsRequired();

        builder.Property(redemption => redemption.PurchaseId)
            .HasConversion(FinancialValueConverters.Id<MonetizationPurchaseId>());

        builder.Property(redemption => redemption.PromotionCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(redemption => redemption.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(redemption => redemption.ProductCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(redemption => redemption.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.HasIndex(redemption => new { redemption.PromotionId, redemption.RedeemedByUserId })
            .HasDatabaseName("ix_financial_redemptions_promotion_user");

        builder.HasIndex(redemption => redemption.RedeemedByUserId)
            .HasDatabaseName("ix_financial_redemptions_user");

        builder.Ignore(redemption => redemption.DomainEvents);
        builder.Ignore(redemption => redemption.Discount);
    }
}

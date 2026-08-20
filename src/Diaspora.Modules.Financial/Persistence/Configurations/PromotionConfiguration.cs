using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps promotions. The discount and the validity window are required owned types.</summary>
public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.PromotionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(promotion => promotion.Id);

        builder.Property(promotion => promotion.Id)
            .HasConversion(FinancialValueConverters.Id<PromotionId>());

        builder.Property(promotion => promotion.Revision).IsConcurrencyToken();

        builder.Property(promotion => promotion.Code)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(promotion => promotion.Name)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(promotion => promotion.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(promotion => promotion.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(promotion => promotion.CountryCodes)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(promotion => promotion.ProductCodes)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(promotion => promotion.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength);

        builder.OwnsOne(promotion => promotion.Discount, discount =>
        {
            discount.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .HasColumnName("DiscountKind")
                .IsRequired();

            discount.Property(value => value.Percentage)
                .HasColumnName("DiscountPercentage")
                .HasPrecision(
                    FinancialPersistenceConstants.RatePrecision,
                    FinancialPersistenceConstants.RateScale);

            discount.Property(value => value.FixedMinorUnits).HasColumnName("DiscountFixedMinorUnits");

            discount.Property(value => value.Currency)
                .HasColumnName("DiscountCurrency")
                .HasMaxLength(FinancialPersistenceConstants.CurrencyLength);

            discount.Property(value => value.MaximumMinorUnits)
                .HasColumnName("DiscountMaximumMinorUnits");
        });

        builder.Navigation(promotion => promotion.Discount).IsRequired();

        builder.OwnsOne(promotion => promotion.Window, window =>
        {
            window.Property(period => period.StartsAt)
                .HasColumnName("StartsAt")
                .IsRequired();

            window.Property(period => period.EndsAt).HasColumnName("EndsAt");
        });

        builder.Navigation(promotion => promotion.Window).IsRequired();

        builder.HasIndex(promotion => promotion.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_promotions_code");

        builder.HasIndex(promotion => promotion.Status)
            .HasDatabaseName("ix_financial_promotions_status");

        builder.Ignore(promotion => promotion.DomainEvents);
    }
}

using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the monetization catalogue.
/// <para>
/// Reference data: a few dozen rows, read on every catalogue request through the cache, written by
/// an operator perhaps monthly. The unique index on the code is what makes the catalogue's duplicate
/// refusal enforceable at the database rather than only at the seeder.
/// </para>
/// </summary>
public sealed class MonetizationProductConfiguration : IEntityTypeConfiguration<MonetizationProduct>
{
    public void Configure(EntityTypeBuilder<MonetizationProduct> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.ProductsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasConversion(FinancialValueConverters.Id<MonetizationProductId>());

        builder.Property(product => product.Revision).IsConcurrencyToken();

        builder.Property(product => product.Code)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(product => product.Category)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(product => product.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(product => product.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(product => product.PriceCurrency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength);

        builder.Property(product => product.PricingPolicyKey)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(product => product.PlacementKind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength);

        builder.Property(product => product.PriorityWeight)
            .HasPrecision(
                FinancialPersistenceConstants.RatePrecision,
                FinancialPersistenceConstants.RateScale);

        builder.OwnsMany(product => product.Eligibility, rule =>
        {
            rule.ToTable(
                FinancialPersistenceConstants.ProductEligibilityTable,
                FinancialModuleMetadata.Schema);

            rule.HasKey(value => value.RuleId);
            rule.Property(value => value.RuleId).ValueGeneratedNever();

            rule.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .IsRequired();

            rule.Property(value => value.Value)
                .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

            rule.HasIndex("MonetizationProductId", nameof(EligibilityRule.Kind))
                .IsUnique()
                .HasDatabaseName("ix_financial_product_eligibility_unique");
        });

        builder.HasIndex(product => product.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_products_code");

        builder.HasIndex(product => new { product.Status, product.SubjectType, product.SortOrder })
            .HasDatabaseName("ix_financial_products_available");

        builder.Ignore(product => product.DomainEvents);
        builder.Ignore(product => product.Price);
    }
}

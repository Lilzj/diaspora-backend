using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps the plan catalogue and the entitlements each plan grants.</summary>
public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.PlansTable, FinancialModuleMetadata.Schema);

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Id)
            .HasConversion(FinancialValueConverters.Id<SubscriptionPlanId>());

        builder.Property(plan => plan.Revision).IsConcurrencyToken();

        builder.Property(plan => plan.Code)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(plan => plan.Name)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(plan => plan.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(plan => plan.Tier)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(plan => plan.Interval)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(plan => plan.AudienceType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(plan => plan.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(plan => plan.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.OwnsMany(plan => plan.Entitlements, entitlement =>
        {
            entitlement.ToTable(
                FinancialPersistenceConstants.PlanEntitlementsTable, FinancialModuleMetadata.Schema);

            entitlement.HasKey(value => value.EntitlementId);
            entitlement.Property(value => value.EntitlementId).ValueGeneratedNever();

            entitlement.Property(value => value.Key)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength)
                .IsRequired();

            entitlement.Property(value => value.Value)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength);

            entitlement.HasIndex("SubscriptionPlanId", nameof(Entitlement.Key))
                .IsUnique()
                .HasDatabaseName("ix_financial_plan_entitlements_unique");
        });

        builder.HasIndex(plan => plan.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_plans_code");

        builder.HasIndex(plan => new { plan.IsActive, plan.AudienceType, plan.SortOrder })
            .HasDatabaseName("ix_financial_plans_active");

        builder.Ignore(plan => plan.DomainEvents);
        builder.Ignore(plan => plan.Price);
        builder.Ignore(plan => plan.IsFree);
        builder.Ignore(plan => plan.EntitlementKeys);
    }
}

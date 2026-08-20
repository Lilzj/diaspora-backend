using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps subscriptions.
/// <para>
/// The entitlements are copied onto the subscription rather than read through to the plan, so this
/// table has its own owned collection. A plan repriced or re-scoped next month must not silently
/// change what somebody is already paying for — the same reasoning that puts a policy version on a
/// ledger entry.
/// </para>
/// </summary>
public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.SubscriptionsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id)
            .HasConversion(FinancialValueConverters.Id<SubscriptionId>());

        builder.Property(subscription => subscription.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(subscription => subscription.PlanId)
            .HasConversion(FinancialValueConverters.Id<SubscriptionPlanId>())
            .IsRequired();

        builder.Property(subscription => subscription.LastPaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>());

        builder.Property(subscription => subscription.Revision).IsConcurrencyToken();

        builder.Property(subscription => subscription.PendingPlanCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(subscription => subscription.PlanCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(subscription => subscription.Tier)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(subscription => subscription.Interval)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(subscription => subscription.EndReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.OwnsOne(subscription => subscription.CurrentPeriod, period =>
        {
            period.Property(window => window.StartsAt)
                .HasColumnName("PeriodStartsAt")
                .IsRequired();

            period.Property(window => window.EndsAt).HasColumnName("PeriodEndsAt");

            period.HasIndex(nameof(DateWindow.EndsAt))
                .HasDatabaseName("ix_financial_subscriptions_period_end");
        });

        builder.Navigation(subscription => subscription.CurrentPeriod).IsRequired();

        builder.OwnsMany(subscription => subscription.Entitlements, entitlement =>
        {
            entitlement.ToTable(
                FinancialPersistenceConstants.SubscriptionEntitlementsTable,
                FinancialModuleMetadata.Schema);

            entitlement.HasKey(value => value.EntitlementId);
            entitlement.Property(value => value.EntitlementId).ValueGeneratedNever();

            entitlement.Property(value => value.Key)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength)
                .IsRequired();

            entitlement.Property(value => value.Value)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength);

            entitlement.HasIndex("SubscriptionId", nameof(Entitlement.Key))
                .IsUnique()
                .HasDatabaseName("ix_financial_subscription_entitlements_unique");
        });

        builder.HasIndex(subscription => new { subscription.SubscriberUserId, subscription.Status })
            .HasDatabaseName("ix_financial_subscriptions_subscriber");

        builder.HasIndex(subscription => subscription.Status)
            .HasDatabaseName("ix_financial_subscriptions_status");

        builder.Ignore(subscription => subscription.DomainEvents);
        builder.Ignore(subscription => subscription.EntitlementKeys);
        builder.Ignore(subscription => subscription.GrantsAccess);
    }
}

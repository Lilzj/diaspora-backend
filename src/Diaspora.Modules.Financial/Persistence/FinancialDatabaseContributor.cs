using Diaspora.Modules.Financial.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence;

/// <summary>
/// Adds the Financial Platform's tables to the shared context.
/// <para>
/// Twenty-six tables in the module's own <c>financial</c> schema, from sixteen configurations —
/// because ten of those tables are owned by an aggregate and configured from inside it, which is the
/// aggregate boundary made visible in the persistence layer.
/// </para>
/// <para>
/// <b>Every table name is prefixed <c>financial_</c>.</b> SQLite ignores schemas and SQLite is the
/// provider every integration test runs on, so two modules mapping one bare name share a table and
/// the failure names a test rather than the modules that collided. PSR-1 made that a build failure;
/// this module was written afterwards and simply avoids it.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>, and that is the whole design rather than an
/// oversight.</b> A financial record is never deleted, softly or otherwise. The ledger is
/// append-only; a payment intent that failed is a fact; a cancelled placement is a fact; a refunded
/// donation is two facts. Where the platform elsewhere reaches for a soft delete, this module
/// appends a compensating record — which is what makes the audit trail an audit trail rather than a
/// current-state table with history bolted on.
/// </para>
/// </summary>
public sealed class FinancialDatabaseContributor : IDatabaseContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public string Schema => FinancialModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Financial identity, and the wallets derived from the ledger.
        modelBuilder.ApplyConfiguration(new FinancialAccountConfiguration());
        modelBuilder.ApplyConfiguration(new WalletConfiguration());

        // The canonical record, and the unit of work over it.
        modelBuilder.ApplyConfiguration(new LedgerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialTransactionConfiguration());

        // Money arriving from outside — owns its attempts and its refunds.
        modelBuilder.ApplyConfiguration(new PaymentIntentConfiguration());

        // The versioned rules every price comes from — owns its parameters.
        modelBuilder.ApplyConfiguration(new FinancialPolicyConfiguration());

        // What can be bought, and what was bought.
        modelBuilder.ApplyConfiguration(new MonetizationProductConfiguration());
        modelBuilder.ApplyConfiguration(new MonetizationPurchaseConfiguration());
        modelBuilder.ApplyConfiguration(new PlacementConfiguration());

        // B16. The layer above a placement — see AdCampaignConfiguration for why the table is
        // financial_ad_campaigns and not financial_campaigns.
        modelBuilder.ApplyConfiguration(new AdCampaignConfiguration());
        modelBuilder.ApplyConfiguration(new AdPerformanceDayConfiguration());

        // Recurring — the plan catalogue and the subscriptions against it.
        modelBuilder.ApplyConfiguration(new SubscriptionPlanConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());

        // Discounts, and the record of every one used.
        modelBuilder.ApplyConfiguration(new PromotionConfiguration());
        modelBuilder.ApplyConfiguration(new PromotionRedemptionConfiguration());
        modelBuilder.ApplyConfiguration(new CreditGrantConfiguration());

        // Giving. Causes and funds are reference data; donations and contributions are records.
        modelBuilder.ApplyConfiguration(new DonationCauseConfiguration());
        modelBuilder.ApplyConfiguration(new DonationConfiguration());
        modelBuilder.ApplyConfiguration(new ContributionFundConfiguration());
        modelBuilder.ApplyConfiguration(new CommunityContributionConfiguration());

        // The measured record, and the currency catalogue everything is denominated in.
        modelBuilder.ApplyConfiguration(new RevenueRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyDefinitionConfiguration());

        // B15.1. Money leaving, money asked for back, and the one callback surface a provider has.
        modelBuilder.ApplyConfiguration(new ProcessedWebhookEventConfiguration());
        modelBuilder.ApplyConfiguration(new PayoutDestinationConfiguration());
        modelBuilder.ApplyConfiguration(new WithdrawalConfiguration());
        modelBuilder.ApplyConfiguration(new RefundRequestConfiguration());

        // Invoices: one per captured payment the platform kept, numbered DSP-yyyy-nnnnnn.
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());

        // Points, which are not money, and the one place they become it.
        modelBuilder.ApplyConfiguration(new RewardAccountConfiguration());
        modelBuilder.ApplyConfiguration(new RewardConversionConfiguration());

        // B15.1 Giving Extension. What a settled payment bought, the fundraising programme around a
        // cause, its timeline, and the standing instruction to give again.
        modelBuilder.ApplyConfiguration(new MembershipPaymentBindingConfiguration());
        modelBuilder.ApplyConfiguration(new CampaignConfiguration());
        modelBuilder.ApplyConfiguration(new CampaignUpdateConfiguration());
        modelBuilder.ApplyConfiguration(new RecurringDonationConfiguration());
    }
}

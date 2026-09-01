using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Entitlements;
using Diaspora.Modules.Financial.Extensibility.Caching;
using Diaspora.Modules.Financial.Extensibility.Campaigns;
using Diaspora.Modules.Financial.Extensibility.Catalog;
using Diaspora.Modules.Financial.Extensibility.Eligibility;
using Diaspora.Modules.Financial.Extensibility.Fraud;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Pricing;
using Diaspora.Modules.Financial.Extensibility.Provisioning;
using Diaspora.Modules.Financial.Extensibility.Receipts;
using Diaspora.Modules.Financial.Extensibility.Search;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Caching;
using Diaspora.Modules.Financial.Infrastructure.Eligibility;
using Diaspora.Modules.Financial.Infrastructure.Fraud;
using Diaspora.Modules.Financial.Infrastructure.Gateways;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using Diaspora.Modules.Financial.Infrastructure.Ledger;
using Diaspora.Modules.Financial.Infrastructure.Pricing;
using Diaspora.Modules.Financial.Infrastructure.Provisioning;
using Diaspora.Modules.Financial.Infrastructure.Receipts;
using Diaspora.Modules.Financial.Infrastructure.Search;
using Diaspora.Modules.Financial.Infrastructure.Settlement;
using Diaspora.Modules.Financial.Options;
using Diaspora.Modules.Financial.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Diaspora.Modules.Financial;

/// <summary>
/// B15's registration.
/// <para>
/// <b>Order 1400, last.</b> The Financial Platform consumes what other modules publish — a
/// registration, a verification, a listing being archived — and is consumed by none of them at
/// composition time. It belongs at the end of the graph, and the seam registries are built in module
/// order, which means a monetization product, a financial policy or a fund contributed by an earlier
/// module is already present when this one scans for them.
/// </para>
/// <para>
/// <b>The seams are scanned; everything else is registered by hand.</b> A payment gateway, a pricing
/// rule, an eligibility rule, a fraud assessor and a catalogue contributor may arrive from any
/// assembly — that is the whole extensibility claim. A repository, the posting service, the payment
/// processor or the identity service is a collaborator of this module's own handlers, and scanning
/// for those would let an accidental second implementation elsewhere silently take over a write path
/// that moves money.
/// </para>
/// <para>
/// <b>The single-implementation seams are last-wins.</b> The gateway registry, the eligibility
/// evaluator, the policy engine, the distribution strategy, the receipt generator, the wallet
/// provisioning policy, the cache and the search projector are all replaceable: a deployment
/// registers its own after this module and <c>AddScoped</c>'s last-wins behaviour does the rest. The
/// plural seams — gateways, webhook processors, pricing rules, eligibility rules, fraud assessors,
/// contributors — are additive and every implementation is available.
/// </para>
/// <para>
/// <b>The ledger posting service is not replaceable and is deliberately not a scanned seam.</b> An
/// architecture test asserts there is exactly one implementation in this assembly, because a second
/// one would be a path where the balancing rule, the wallet status check, the sequence assignment and
/// the balance-before check do not apply.
/// </para>
/// </summary>
public sealed class FinancialModule : IPlatformModule
{
    public string Name => FinancialModuleMetadata.Name;

    public string Version => FinancialModuleMetadata.Version;

    public int Order => 1400;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(FinancialModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<FinancialOptions>()
            .Bind(configuration.GetSection(FinancialOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The payout capability matrix. Validated on start like every other module option, which
        // catches configuration that cannot bind at all — a misspelled payout method, a priority
        // that is not a number. Row-level sense is checked later and separately: a three-letter
        // country code drops that one route in ConfiguredPayoutCapabilitySource and is logged,
        // because it should cost one destination rather than every request the API serves.
        services.AddOptions<PayoutRoutingOptions>()
            .Bind(configuration.GetSection(PayoutRoutingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IFinancialAccountRepository, FinancialAccountRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IFinancialTransactionRepository, FinancialTransactionRepository>();
        services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();
        services.AddScoped<IFinancialPolicyRepository, FinancialPolicyRepository>();
        services.AddScoped<IMonetizationProductRepository, MonetizationProductRepository>();
        services.AddScoped<IMonetizationPurchaseRepository, MonetizationPurchaseRepository>();
        services.AddScoped<IPlacementRepository, PlacementRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IPromotionRedemptionRepository, PromotionRedemptionRepository>();
        services.AddScoped<ICreditGrantRepository, CreditGrantRepository>();
        services.AddScoped<IDonationCauseRepository, DonationCauseRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IContributionFundRepository, ContributionFundRepository>();
        services.AddScoped<ICommunityContributionRepository, CommunityContributionRepository>();
        services.AddScoped<IRevenueRecordRepository, RevenueRecordRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();

        // B15.1.
        services.AddScoped<IProcessedWebhookEventRepository, ProcessedWebhookEventRepository>();
        services.AddScoped<IPayoutDestinationRepository, PayoutDestinationRepository>();
        services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
        services.AddScoped<IRefundRequestRepository, RefundRequestRepository>();
        services.AddScoped<IRewardAccountRepository, RewardAccountRepository>();
        services.AddScoped<IRewardConversionRepository, RewardConversionRepository>();

        // B15.1 Giving Extension. COMMUNITY-LIFE-GAP-003, -006, -007 and -008.
        services.AddScoped<IMembershipPaymentBindingRepository, MembershipPaymentBindingRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ICampaignUpdateRepository, CampaignUpdateRepository>();
        services.AddScoped<IRecurringDonationRepository, RecurringDonationRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<FinancialAuthorizationService>();
        services.AddScoped<CurrencyCatalog>();

        // The one door into "this owner now has somewhere to keep money". Registered explicitly
        // rather than scanned because a second implementation appearing by accident would provision
        // a different set of wallets and nobody would notice until a balance was missing.
        services.AddScoped<FinancialIdentityService>();

        // Drives a payment through a gateway and writes what happened into the ledger.
        services.AddScoped<PaymentProcessor>();

        // The single translation point where a domain fact becomes a published contract.
        services.AddScoped<FinancialIntegrationEventRelay>();

        // The one door into the ledger. An architecture test asserts there is exactly one
        // implementation in this assembly — see ILedgerPostingService for why that matters.
        services.AddScoped<ILedgerPostingService, LedgerPostingService>();

        // The points seam a module outside B15 redeems through, added in Wave 6 so the Creator
        // Platform could spend points without owning a balance. Registered here rather than
        // discovered, because a second implementation appearing by accident would be a second answer
        // to "how many points does this member have" — see IRewardPointsAccount.
        services.AddScoped<
            Extensibility.Rewards.IRewardPointsAccount,
            Application.Rewards.RewardPointsAccount>();

        // Resolves what a member may move, from policy rather than from a constant in a handler.
        // Registered explicitly: a second implementation appearing by accident would be a second set
        // of deposit and withdrawal limits, and whichever one a handler happened to resolve would win.
        services.AddScoped<FinancialLimitsResolver>();

        // Holds and releases money for a payout. The only caller of the ledger on the withdrawal
        // path, so the hold and its release cannot be written two different ways.
        services.AddScoped<WithdrawalProcessor>();

        // Prices a points conversion. One implementation, read by both the preview and the
        // conversion — two code paths computing one rate is how a preview comes to disagree with a
        // charge, and the disagreement is only ever noticed by the member it shortchanged.
        services.AddScoped<Application.Rewards.RewardConversionPricer>();

        // Resolves what a member is entitled to and how much of it they have used.
        services.AddScoped<EntitlementResolver>();

        // B23.x Wave 12. This module's answer to the two automation conditions about money —
        // `walletBalance` and `subscriptionActive`. Read-only, and registered here rather than in the
        // Automation Platform because a balance is this module's to report: the seam is declared in
        // Diaspora.Contracts precisely so an owning module can implement it without a forward
        // reference to a module composed after it.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationConditionSource,
            Extensibility.Automation.FinancialAutomationConditionSource>();

        // Prices a plan change. One implementation, read by the preview and by the change — two
        // computations of one proration is how a preview and a charge come to disagree.
        services.AddScoped<Application.Subscriptions.SubscriptionChangePricer>();
    }

    /// <summary>
    /// The extension points, scanned across every platform assembly.
    /// <para>
    /// <b>The plural seams are additive.</b> Every registered gateway is available to the registry,
    /// every pricing rule to the engine, every eligibility rule to the evaluator, every fraud
    /// assessor to the payment path, and every contributor to the seeder. A module adding one does
    /// not displace anything.
    /// </para>
    /// <para>
    /// <b>The singular seams are last-wins.</b> A deployment replaces the gateway registry, the
    /// eligibility evaluator, the policy engine, the distribution strategy, the receipt generator,
    /// the provisioning policy, the cache or the search projector by registering its own after this
    /// module — which is what <c>AddScoped</c> gives, and is how a real payment provider, a
    /// distributed cache or an OpenSearch adapter would land without a line of this module changing.
    /// </para>
    /// <para>
    /// <b>Two seams are deliberately left unimplemented.</b> <c>IExchangeRateProvider</c> and
    /// <c>ISettlementProvider</c> have no default: the platform never converts and no settlement
    /// engine ships in this phase, and registering an implementation that did nothing would make
    /// <c>GET /financial/capabilities</c> claim a capability the deployment does not have.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        // Reference data. Any module may contribute currencies, policies, products, plans and funds
        // without this module referencing it.
        RegisterAll<ICurrencyContributor>(services, assemblies);
        RegisterAll<IFinancialPolicyContributor>(services, assemblies);
        RegisterAll<IMonetizationProductContributor>(services, assemblies);
        RegisterAll<ISubscriptionPlanContributor>(services, assemblies);
        RegisterAll<IContributionFundContributor>(services, assemblies);

        // Payments. Only this assembly is scanned, and that is load-bearing rather than tidy.
        //
        // A vendor adapter lives in its own assembly — Diaspora.Payments.Stripe and its siblings —
        // and every one of them is named Diaspora.*, so a platform-wide scan finds them. RegisterAll
        // registers what it finds unconditionally: it cannot ask whether an adapter has credentials,
        // because IsConfigured is the adapter's own business. So merely *referencing* Stripe would
        // register its gateway on a deployment holding no Stripe key, and three things break at once.
        //
        // The capabilities endpoint builds `gateways` from the registry, so it would report `stripe`
        // to every client on a deployment that cannot take a single payment — the exact inversion of
        // the honest empty answer it gives today. Every payment would then reach Stripe without a
        // key and come back 401, which reads to a member as a platform fault. And a scan-built
        // instance is activated by the container, so it would be handed the default HttpClient
        // rather than the named one carrying the configured timeout.
        //
        // This is the same defect that was found and fixed on ISettlementProvider, which is why no
        // RegisterAll line for that seam appears below. Deployments register their own adapters, in
        // Program.cs, behind their own IsConfigured check.
        //
        // The shipped simulator still comes from here because it lives here — and it stays
        // Development and Testing only. See IsDevelopmentOnlyPaymentAdapter for what happened when
        // it did not.
        var shipped = (IReadOnlyList<Assembly>)[typeof(FinancialModule).Assembly];

        RegisterAll<IPaymentGateway>(services, shipped, ExcludeDevelopmentOnlyPaymentAdapters(services));
        RegisterAll<IPaymentWebhookProcessor>(services, shipped, ExcludeDevelopmentOnlyPaymentAdapters(services));
        RegisterAll<IPaymentReconciliationProvider>(services, shipped);
        services.AddScoped<IPaymentGatewayRegistry, PaymentGatewayRegistry>();

        // Payouts. Sources are additive so a deployment can overlay its own matrix on a shipped one;
        // the router collapses conflicts toward the more cautious row. Scoped like every other seam
        // here — the router reads IOptionsMonitor rather than caching, so a matrix edit reaches the
        // next withdrawal rather than the next deployment.
        RegisterAll<IPayoutCapabilitySource>(services, assemblies);
        services.AddScoped<PayoutProviderRouter>();
        services.AddScoped<PayoutRouteResolver>();

        // None of IPayoutProviderVerificationSource, ISettlementProvider, ISettlementStatusProvider
        // or IPayoutWebhookProcessor is scanned for. All are vendor adapters, and an adapter is
        // registered by the deployment with the named HTTP client, options monitor and credential
        // cache it needs — a type-scanned AddScoped(typeof(T)) cannot build one, and where it
        // happens to succeed it builds the wrong one.
        //
        // The resolver and the router both take IEnumerable<>, so no registration means no provider,
        // which means a payout refuses rather than being attempted. That is the safe direction.

        // Pricing. Every rule is offered the context; the first that claims it answers.
        RegisterAll<IPricingRule>(services, assemblies);
        services.AddScoped<IFinancialPolicyEngine, FinancialPolicyEngine>();

        // Eligibility. Every rule is available; the product's own configuration decides which run.
        RegisterAll<IProductEligibilityRule>(services, assemblies);
        services.AddScoped<IProductEligibilityEvaluator, ProductEligibilityEvaluator>();

        // Fraud. Additive, and the strictest decision wins — so a model runs alongside the shipped
        // velocity rules rather than instead of them.
        RegisterAll<IFraudAssessor>(services, assemblies);

        // Entitlement consumption. Additive, and every provider owns one vertical's counters: only
        // Jobs knows how many job posts a recruiter has open this month, and B15 must not guess.
        // B15 serves the ceiling, which it does own, whether or not anybody reports the consumption.
        RegisterAll<IEntitlementUsageProvider>(services, assemblies);

        // Settlement is NOT scanned for, unlike every other plural seam above. It used to be, on the
        // reasoning that an architecture test keeps this assembly empty of implementations so the
        // scan could only ever find a deployment's own — which was true right up until a deployment
        // referenced an adapter assembly.
        //
        // Then the scan found it and registered it by type, whether or not it was configured. That
        // is three failures at once: an unconfigured adapter tells the payout router the rail is
        // available, a scan-built instance gets the default HttpClient rather than the named one
        // with its timeout, and an adapter needing anything the container cannot supply — PayPal's
        // access-token cache — fails host startup outright. The last is how it was found.
        //
        // A deployment registers its own; see AddStripePayments, AddPayPalPayments and
        // AddFlutterwavePayments, each a no-op until its credentials exist.

        // The replaceable singles. See the method summary.
        services.AddScoped<IRevenueDistributionStrategy, PlatformCommissionDistributionStrategy>();
        services.AddScoped<IWalletProvisioningPolicy, DefaultWalletProvisioningPolicy>();
        services.AddScoped<IReceiptReferenceGenerator, SequentialReceiptReferenceGenerator>();
        services.AddScoped<IFinancialSearchProjector, NoOpFinancialSearchProjector>();

        // A singleton, and safe as such: a concurrent dictionary, no scoped dependency, and the one
        // component whose whole value is that it outlives a request.
        services.AddSingleton<IFinancialCache, InMemoryFinancialCache>();

        // ── B15.1 Giving Extension ───────────────────────────────────────────────────────────
        //
        // Two seams into modules this one does not reference, and both refuse by default. The
        // owning module composes after this one and registers the real implementation, which
        // AddScoped's last-wins behaviour then resolves — the same replaceable-single arrangement
        // the gateway registry, the receipt generator and the policy engine already use.
        //
        // **Registered by hand rather than scanned, and that distinction cost a debugging cycle.**
        // ModuleAssemblyScanner finds every implementation in every platform assembly — including
        // the refusing default in this one — and registers them in assembly order.
        // "Diaspora.Modules.CommunityProgramme" sorts before "Diaspora.Modules.Financial", so the
        // scan registered the real authority first and the denier second, and last-wins made the
        // platform refuse every organization campaign. Nothing errored; a capability seam simply
        // said no. A null object must never be in the same scanned set as the thing it stands in
        // for.
        services.AddScoped<ICampaignOrganizerAuthority, DenyingCampaignOrganizerAuthority>();
        services.AddScoped<
            Extensibility.Memberships.IMembershipSubjectAuthority,
            Infrastructure.Memberships.UnknownMembershipSubjectAuthority>();

        services.AddScoped<Application.Campaigns.CampaignAccessService>();

        // The one thing on this platform that decides whether a payment bought a membership. A
        // service rather than a handler because the route that needs it belongs to another module
        // and DSP1001 refuses a handler that can reach another handler — see the class summary.
        services.AddScoped<Application.Memberships.MembershipPaymentBinder>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<FinancialCatalogSeeder>();

        // The ordering between these is a design decision rather than an accident of two timers:
        // the placement sweep runs more often than the revenue rollup, so a rollup never counts a
        // placement the featured rail has already dropped. See B12's handbook entry.
        services.AddHostedService<PaymentExpirySweep>();
        services.AddHostedService<PlacementLifecycleSweep>();
        services.AddHostedService<SubscriptionRenewalSweep>();
        services.AddHostedService<CreditExpirySweep>();
        services.AddHostedService<RevenueRollupSweep>();
        services.AddHostedService<GivingReconciliationSweep>();

        // B15.1. The expiry warning runs before the placement sweep that ends things, so a member is
        // warned that a promotion is about to lapse rather than told it already has.
        services.AddHostedService<ExpiryNoticeSweep>();
        services.AddHostedService<WithdrawalProcessingSweep>();
        services.AddHostedService<PaymentReconciliationSweep>();
        services.AddHostedService<PayoutReconciliationSweep>();

        // B15.1 Giving Extension. The campaign sweep runs before the recurring charge, so a campaign
        // whose deadline passed overnight has closed its cause by the time a standing donation would
        // have given to it — a member should not be charged for a campaign that stopped collecting.
        services.AddHostedService<GivingLifecycleSweep>();
        services.AddHostedService<RecurringDonationSweep>();
        services.AddHostedService<MembershipBindingReconciliationSweep>();
    }

    private static void RegisterAll<TContract>(
        IServiceCollection services, IReadOnlyList<Assembly> assemblies, Func<Type, bool>? include = null)
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            if (include is not null && !include(type))
            {
                continue;
            }

            services.AddScoped(typeof(TContract), type);
        }
    }

    /// <summary>
    /// Keeps the shipped payment simulator out of every environment but Development and Testing.
    /// <para>
    /// <b>The simulator's webhook secret is a public compile-time constant.</b>
    /// <c>SimulatedPaymentWebhookProcessor.Sign</c> defaults to
    /// <c>DevelopmentSecret</c>, <c>IsSignatureValid</c> calls it with that default, and no
    /// configuration key changes it. <c>POST /financial/webhooks/{provider}</c> is anonymous by
    /// design — for a real provider the signature <em>is</em> the credential — so where that
    /// signature can be computed by anyone holding the source, the route is an unauthenticated
    /// write to the payment state machine.
    /// </para>
    /// <para>
    /// <b>That is not a theoretical reading.</b> Driven end to end against a release candidate: a
    /// member created a payment intent, and an unauthenticated caller carrying nothing but the
    /// constant and the intent's identifier moved it <c>Draft → Authorized → Captured</c>, both
    /// callbacks answering <c>outcome=Applied</c>. Four seconds later the platform posted a
    /// completed <c>Payment</c> transaction of 25,000.00 NGN and a double-entry ledger pair —
    /// Clearing debited, Revenue credited — for money that had never moved.
    /// </para>
    /// <para>
    /// The surrounding controls all held: an unsigned callback was refused, a wrongly signed one
    /// was refused, a replayed event identifier was de-duplicated, and an unknown reference was
    /// answered <c>Unmatched</c> without inventing a payment. None of them can help, because the
    /// callback was correctly signed. The only fix is for the adapter that can be forged not to be
    /// there.
    /// </para>
    /// <para>
    /// So in any other environment the provider simply does not exist: the webhook route answers
    /// <c>404 financial.webhook.provider_unknown</c>, and
    /// <c>GET /financial/capabilities</c> reports no gateway rather than naming one that cannot
    /// take money. A deployment that means to accept payments registers a real adapter, which is
    /// what the seam is for. This is the same position the platform already takes on
    /// <c>Notifications:Push:Provider = "Loopback"</c>, and for the same reason: admitting nothing
    /// is configured beats reporting success that did not happen.
    /// </para>
    /// </summary>
    private static Func<Type, bool> ExcludeDevelopmentOnlyPaymentAdapters(IServiceCollection services)
    {
        // The host registers IHostEnvironment as a singleton instance before any module composes,
        // so it is readable from the collection without building a provider. If it cannot be read
        // the simulator is excluded: an environment this code cannot identify is not one that
        // should be given a forgeable payment adapter.
        var environment = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostEnvironment))
            ?.ImplementationInstance as IHostEnvironment;

        var simulationPermitted =
            environment is not null
            && (environment.IsDevelopment() || environment.IsEnvironment("Testing"));

        return type => simulationPermitted || !IsDevelopmentOnlyPaymentAdapter(type);
    }

    private static bool IsDevelopmentOnlyPaymentAdapter(Type type) =>
        type == typeof(SimulatedPaymentGateway) || type == typeof(SimulatedPaymentWebhookProcessor);
}

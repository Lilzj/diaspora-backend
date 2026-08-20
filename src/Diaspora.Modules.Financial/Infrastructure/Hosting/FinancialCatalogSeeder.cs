using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Extensibility.Catalog;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Seeds everything the Financial Platform cannot function without.
/// <para>
/// <b>The platform's own financial account comes first and everything else depends on it.</b> It is
/// the counterparty to every transaction: a capture debits its clearing wallet, a commission credits
/// its revenue wallet, a credit grant debits its promotional one. Without it, the very first payment
/// fails with an error about a missing wallet that names nothing useful.
/// </para>
/// <para>
/// <b>A hosted service with a public, swallowing <see cref="SeedAsync"/>.</b> Both halves of that
/// shape are a contract this platform has paid for twice: the service runs at host start, possibly
/// before migrations have been applied, and throwing there takes the whole host down along with every
/// other module's startup. The integration suite creates its schema after the host starts and calls
/// <see cref="SeedAsync"/> again, which is why the method is public and why seeding is idempotent by
/// contract.
/// </para>
/// <para>
/// <b>Seeding creates and never overwrites.</b> A commission rate an operator retuned, a product they
/// repriced and a fund they renamed all survive a restart — the alternative would make the whole
/// measure-and-adjust loop pointless.
/// </para>
/// </summary>
public sealed class FinancialCatalogSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<FinancialCatalogSeeder> _logger;

    public FinancialCatalogSeeder(
        IServiceScopeFactory scopes, ILogger<FinancialCatalogSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Module => FinancialModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    /// <summary>
    /// Brings the catalogue up to date. Idempotent, and swallows everything.
    /// <para>
    /// Public because a host that applies migrations separately — which the integration suite does —
    /// has to be able to call it again once the schema exists.
    /// </para>
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var provider = scope.ServiceProvider;

            await SeedCurrenciesAsync(provider, cancellationToken);
            await SeedPlatformAccountAsync(provider, cancellationToken);
            await SeedPoliciesAsync(provider, cancellationToken);
            await SeedProductsAsync(provider, cancellationToken);
            await SeedPlansAsync(provider, cancellationToken);
            await SeedFundsAsync(provider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Never take the host down. A platform whose sign-in is broken because a currency list
            // was already present is a worse outcome than an incomplete catalogue.
            _logger.LogError(
                exception,
                "Seeding the financial catalogue failed. The platform will start with whatever is "
                + "already in the database; call SeedAsync again once the schema exists.");
        }
    }

    private async Task SeedCurrenciesAsync(
        IServiceProvider provider, CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<ICurrencyRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var added = 0;

        foreach (var contributor in provider.GetServices<ICurrencyContributor>())
        {
            foreach (var seed in contributor.GetCurrencies())
            {
                if (await repository.ExistsAsync(seed.Code, cancellationToken))
                {
                    continue;
                }

                repository.Add(CurrencyDefinition.Define(
                    CurrencyDefinitionId.From(guids.NewSequentialGuid()),
                    seed.Code,
                    seed.Name,
                    seed.Symbol,
                    seed.Exponent,
                    clock.UtcNow,
                    seed.SortOrder));

                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} currencies.", added);
        }
    }

    /// <summary>
    /// Opens the platform's own account and its four wallets, in every seeded currency.
    /// <para>
    /// Every currency, because the clearing wallet a payment needs is the one in the currency the
    /// payment is denominated in — and discovering that on the first Naira transaction would be
    /// discovering it at the worst moment.
    /// </para>
    /// </summary>
    private async Task SeedPlatformAccountAsync(
        IServiceProvider provider, CancellationToken cancellationToken)
    {
        var accounts = provider.GetRequiredService<IFinancialAccountRepository>();
        var currencies = provider.GetRequiredService<ICurrencyRepository>();
        var identities = provider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var options = provider.GetRequiredService<IOptions<FinancialOptions>>().Value;

        var platform = await accounts.FindPlatformAccountAsync(cancellationToken);

        if (platform is null)
        {
            platform = FinancialAccount.OpenPlatform(
                FinancialAccountId.From(guids.NewSequentialGuid()),
                Money.NormalizeCurrency(options.DefaultCurrency) ?? "GBP",
                clock.UtcNow);

            accounts.Add(platform);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Opened the platform's financial account.");
        }

        foreach (var currency in await currencies.ListActiveAsync(cancellationToken))
        {
            var wallets = await identities.EnsureWalletsAsync(
                platform, currency.Code, cancellationToken);

            foreach (var wallet in wallets.Where(wallet => wallet.Status == WalletStatus.Pending))
            {
                wallet.Activate(clock.UtcNow);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPoliciesAsync(
        IServiceProvider provider, CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<IFinancialPolicyRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var added = 0;

        foreach (var contributor in provider.GetServices<IFinancialPolicyContributor>())
        {
            foreach (var seed in contributor.GetPolicies())
            {
                var existing = await repository.HighestVersionAsync(
                    seed.PolicyKey, seed.Scope, seed.ScopeValue, cancellationToken);

                if (existing > 0)
                {
                    continue;
                }

                var policy = FinancialPolicy.Publish(
                    FinancialPolicyId.From(guids.NewSequentialGuid()),
                    seed.PolicyKey,
                    seed.Kind,
                    seed.Scope,
                    seed.ScopeValue,
                    version: 1,
                    DateWindow.Open(clock.UtcNow),
                    seed.Description,
                    clock.UtcNow,
                    seed.IsSystem);

                foreach (var (key, value) in seed.Parameters)
                {
                    policy.SetParameter(guids.NewSequentialGuid(), key, value);
                }

                repository.Add(policy);
                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} financial policies.", added);
        }
    }

    private async Task SeedProductsAsync(
        IServiceProvider provider, CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<IMonetizationProductRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var added = 0;

        foreach (var contributor in provider.GetServices<IMonetizationProductContributor>())
        {
            foreach (var seed in contributor.GetProducts())
            {
                if (await repository.ExistsAsync(seed.Code, cancellationToken))
                {
                    continue;
                }

                var product = MonetizationProduct.Define(
                    MonetizationProductId.From(guids.NewSequentialGuid()),
                    seed.Code,
                    seed.Name,
                    seed.Description,
                    seed.Category,
                    seed.SubjectType,
                    clock.UtcNow,
                    seed.IsSystem);

                var price = seed.PriceAmount is { } amount && seed.Currency is { } currency
                    ? Money.FromAmount(amount, currency)
                    : null;

                product.SetPrice(price, seed.PricingPolicyKey, clock.UtcNow);

                product.SetGrant(
                    seed.PlacementKind,
                    seed.DurationDays,
                    seed.PriorityWeight,
                    seed.IsRenewable,
                    clock.UtcNow);

                product.SetPresentation(seed.Name, seed.Description, seed.SortOrder, clock.UtcNow);

                foreach (var (kind, value) in seed.Eligibility)
                {
                    product.AddEligibility(guids.NewSequentialGuid(), kind, value);
                }

                product.Activate(clock.UtcNow);

                repository.Add(product);
                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} monetization products.", added);
        }
    }

    private async Task SeedPlansAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<ISubscriptionPlanRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var added = 0;

        foreach (var contributor in provider.GetServices<ISubscriptionPlanContributor>())
        {
            foreach (var seed in contributor.GetPlans())
            {
                if (await repository.ExistsAsync(seed.Code, cancellationToken))
                {
                    continue;
                }

                var plan = SubscriptionPlan.Define(
                    SubscriptionPlanId.From(guids.NewSequentialGuid()),
                    seed.Code,
                    seed.Name,
                    seed.Description,
                    seed.Tier,
                    seed.Interval,
                    Money.FromAmount(seed.PriceAmount, seed.Currency),
                    clock.UtcNow,
                    seed.TrialDays,
                    seed.IsSystem,
                    seed.AudienceType,
                    seed.CountryCode);

                plan.SetPresentation(seed.Name, seed.Description, seed.SortOrder, clock.UtcNow);

                foreach (var (key, value) in seed.Entitlements)
                {
                    plan.Grant(guids.NewSequentialGuid(), key, value);
                }

                repository.Add(plan);
                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} subscription plans.", added);
        }
    }

    private async Task SeedFundsAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<IContributionFundRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var guids = provider.GetRequiredService<IGuidGenerator>();
        var clock = provider.GetRequiredService<IClock>();
        var added = 0;

        foreach (var contributor in provider.GetServices<IContributionFundContributor>())
        {
            foreach (var seed in contributor.GetFunds())
            {
                if (await repository.ExistsAsync(seed.Code, cancellationToken))
                {
                    continue;
                }

                var fund = ContributionFund.Establish(
                    ContributionFundId.From(guids.NewSequentialGuid()),
                    seed.Code,
                    seed.Name,
                    seed.Description,
                    seed.Currency,
                    clock.UtcNow,
                    seed.IsSystem,
                    seed.CountryCode);

                if (seed.AcceptedKinds.Count > 0)
                {
                    fund.SetAcceptedKinds(seed.AcceptedKinds);
                }

                fund.SetPresentation(
                    seed.Name, seed.Description, seed.Urgency, seed.SortOrder, null, clock.UtcNow);

                repository.Add(fund);
                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} community funds.", added);
        }
    }
}

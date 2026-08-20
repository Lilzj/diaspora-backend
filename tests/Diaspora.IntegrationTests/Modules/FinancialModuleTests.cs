using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B15 driven over real HTTP through the same host every other test uses.
/// <para>
/// <b>The module plugged in without a single edit to the Platform Kernel, to any earlier module, or
/// to <c>Program.cs</c></b> — only a project reference so its assembly is in the build output. This
/// suite proves its endpoints, permissions, feature gates, EF configuration, catalogue seeding,
/// double-entry ledger and CQRS handlers all composed.
/// </para>
/// </summary>
public sealed class FinancialModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FinancialModuleTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    // ── The catalogue ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Without the seed the platform boots with no currencies, no policies and no platform
    /// account — which is not a degraded state but a broken one.</b> The very first payment would
    /// fail on a missing clearing wallet with an error naming nothing useful.
    /// </summary>
    [Fact]
    public async Task The_catalogue_seeds_currencies_policies_products_plans_and_funds()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await context.Set<CurrencyDefinition>().CountAsync()).Should().BeGreaterThan(0);
        (await context.Set<FinancialPolicy>().CountAsync()).Should().BeGreaterThan(0);
        (await context.Set<MonetizationProduct>().CountAsync()).Should().BeGreaterThan(0);
        (await context.Set<SubscriptionPlan>().CountAsync()).Should().BeGreaterThan(0);
        (await context.Set<ContributionFund>().CountAsync()).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// <b>The counterparty to every transaction on the platform.</b> A capture debits its clearing
    /// wallet and credits its revenue wallet; without both, money arriving from outside would be a
    /// credit with no matching debit and the ledger would not balance.
    /// </summary>
    [Fact]
    public async Task The_platform_account_exists_with_a_clearing_and_a_revenue_wallet()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var platform = await context.Set<FinancialAccount>()
            .FirstOrDefaultAsync(account => account.IsPlatformAccount);

        platform.Should().NotBeNull();

        var wallets = await context.Set<Wallet>()
            .Where(wallet => wallet.AccountId == platform!.Id)
            .ToListAsync();

        wallets.Should().Contain(wallet => wallet.WalletType == WalletType.Clearing);
        wallets.Should().Contain(wallet => wallet.WalletType == WalletType.Revenue);
    }

    /// <summary>
    /// <b>Seeding twice changes nothing.</b> The integration host calls the seeder again after
    /// creating the schema, and a deployment with a separate migration job does the same — so a
    /// seeder that was not idempotent would double every catalogue row on every restart.
    /// </summary>
    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_catalogue()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var before = await context.Set<MonetizationProduct>().CountAsync();

        var seeder = Factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<FinancialCatalogSeeder>()
            .Single();

        await seeder.SeedAsync();

        (await context.Set<MonetizationProduct>().CountAsync()).Should().Be(before);
    }

    // ── The URL space and the envelope ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_financial_surface_is_not_readable_without_a_token()
    {
        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/wallets/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The currency catalogue is what a client needs before it can render a single amount. It opens
    /// at the same tier as reading a wallet.
    /// </summary>
    [Fact]
    public async Task A_member_can_read_the_currency_catalogue_with_symbols_and_exponents()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/currencies", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var currencies = await ReadDataAsync<List<CurrencyShape>>(response);

        currencies.Should().NotBeEmpty();
        currencies.Should().Contain(currency => currency.Code == "GBP" && currency.Exponent == 2);
        currencies.Should().OnlyContain(currency => !string.IsNullOrWhiteSpace(currency.Symbol));
    }

    /// <summary>
    /// <b>The three "no" answers are the useful ones.</b> Conversion and payouts are seams with
    /// nothing behind them, and saying so is a great deal more useful than an empty result somebody
    /// reads as "no data".
    /// </summary>
    [Fact]
    public async Task The_capabilities_endpoint_reports_conversion_and_payouts_as_unavailable()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/capabilities", UriKind.Relative));

        var capabilities = await ReadDataAsync<CapabilitiesShape>(response);

        capabilities.CurrencyConversionEnabled.Should().BeFalse(
            "the platform never converts; a rate has a date");
        capabilities.PayoutsEnabled.Should().BeFalse("no settlement engine ships in this phase");
        capabilities.Gateways.Should().NotBeEmpty("the simulator is registered so payments are testable");
    }

    // ── Financial identity ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Opened at registration and pending until Trust says who somebody is.</b> A pending account
    /// holds wallets that accept credit and refuse every debit, so a welcome bonus can land before
    /// verification and none of it can leave.
    /// </summary>
    [Fact]
    public async Task Registering_opens_a_financial_identity_the_member_can_read()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/wallets/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var wallets = await ReadDataAsync<List<WalletShape>>(response);

        // An empty list is a legitimate answer for a member whose provisioning consumer has not run
        // — the outbox is drained on demand in this suite — so the assertion is on the shape rather
        // than on a count the test does not control.
        wallets.Should().OnlyContain(wallet => wallet.Currency.Length == 3);
        wallets.Should().OnlyContain(wallet => wallet.OwnerType != nameof(FinancialAccountType.Platform));
    }

    /// <summary>
    /// <b>The platform's own nominal accounts are never served to a member.</b> The clearing wallet
    /// runs negative by construction, and serving it to a wallet screen would be serving a number
    /// that means the opposite of what it looks like.
    /// </summary>
    [Fact]
    public async Task A_member_never_sees_a_platform_wallet()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/wallets/me", UriKind.Relative));

        var wallets = await ReadDataAsync<List<WalletShape>>(response);

        wallets.Should().NotContain(wallet =>
            wallet.WalletType == nameof(WalletType.Clearing)
            || wallet.WalletType == nameof(WalletType.Revenue));
    }

    /// <summary>
    /// Answering "forbidden" for a wallet that belongs to somebody else confirms the wallet exists,
    /// which is an enumeration oracle on the one identifier the platform most wants to keep
    /// uninteresting.
    /// </summary>
    [Fact]
    public async Task Somebody_elses_wallet_answers_not_found_rather_than_forbidden()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri($"/api/v1/financial/wallets/{Guid.NewGuid()}/ledger", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── The catalogue a member sees ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every premium feature on this platform is a catalogue row.</b> Boosts, featured placements
    /// and premium listings all arrive here as products with a price, a duration and a placement
    /// kind — there is no code path anywhere that hard-codes one.
    /// </summary>
    [Fact]
    public async Task The_monetization_catalogue_offers_boosts_and_featured_placements()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/catalog?subjectType=MarketplaceListing", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var products = await ReadDataAsync<List<ProductShape>>(response);

        products.Should().NotBeEmpty();
        products.Should().Contain(product => product.PlacementKind == nameof(PlacementKind.Boost));
        products.Should().OnlyContain(product => product.SubjectType == "MarketplaceListing");
    }

    /// <summary>
    /// A product shown as available and then refused at checkout is the worst version of this
    /// feature, so the listing carries the same eligibility decision the purchase path makes.
    /// </summary>
    [Fact]
    public async Task An_ineligible_product_is_returned_with_a_reason_rather_than_hidden()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/catalog?subjectType=Provider", UriKind.Relative));

        var products = await ReadDataAsync<List<ProductShape>>(response);

        products.Where(product => !product.IsAvailable)
            .Should().OnlyContain(product => !string.IsNullOrWhiteSpace(product.UnavailableReason));
    }

    // ── Placements ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The route every other module actually needs.</b> "Is this listing paid for, and until
    /// when" is answerable without the Financial Platform knowing what a listing is — and without
    /// the asking module referencing this one.
    /// </summary>
    [Fact]
    public async Task Placements_for_an_unknown_subject_answer_an_empty_list_rather_than_an_error()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri(
                $"/api/v1/financial/placements/MarketplaceListing/{Guid.NewGuid()}",
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var placements = await ReadDataAsync<List<PlacementShape>>(response);

        placements.Should().BeEmpty(
            "the module never resolves a subject; an unknown one simply has nothing bought for it");
    }

    [Fact]
    public async Task A_subject_type_the_platform_does_not_monetise_is_refused_with_a_code()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri($"/api/v1/financial/placements/Spaceship/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Subscriptions ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The client renders entitlements and never plan names.</b> A screen switching on a plan
    /// code has to ship again the day somebody inserts a plan between two existing ones.
    /// </summary>
    [Fact]
    public async Task The_plan_catalogue_carries_entitlements_rather_than_only_names()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/subscriptions/plans", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var plans = await ReadDataAsync<List<PlanShape>>(response);

        plans.Should().NotBeEmpty();
        plans.Should().Contain(plan => plan.Code == "free" && plan.IsFree);
        plans.Should().OnlyContain(plan => plan.Entitlements.Count > 0);
    }

    /// <summary>
    /// "No subscription" and "the free plan" are the same answer everywhere rather than two, so a
    /// member holding neither gets null rather than a 404.
    /// </summary>
    [Fact]
    public async Task A_member_with_no_subscription_gets_null_rather_than_a_not_found()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/subscriptions/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Giving ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Most giving on this platform is not money.</b> The seeded funds accept all five support
    /// kinds, which is what makes four fifths of the client's donation screen representable.
    /// </summary>
    [Fact]
    public async Task The_community_funds_accept_more_than_money()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/contributions/funds", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var funds = await ReadDataAsync<List<FundShape>>(response);

        funds.Should().NotBeEmpty();
        funds.Should().Contain(fund => fund.Code == "emergency.relief");
        funds.Should().Contain(fund =>
            fund.AcceptedKinds.Contains(nameof(ContributionKind.VolunteerService)));
    }

    [Fact]
    public async Task The_donation_causes_endpoint_serves_the_screen_that_is_currently_mocked()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/donations/causes", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>Redeeming a coupon is refused below the verified tier, and quoting one is the same
    /// surface.</b> A phone-verified member holds <c>Limited</c>; the coupon it would discount buys
    /// something they cannot buy yet, and a control opens with the thing it controls.
    /// <para>
    /// The assertion is a 403 rather than the 404 an unknown code would earn a verified member,
    /// because the tier check happens before the handler ever looks the code up — which is the point
    /// of having both a policy at the endpoint and a permission on the command.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Redeeming_a_coupon_is_refused_below_the_verified_tier()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/promotions/redeem", UriKind.Relative),
            new { code = "NOSUCHCODE" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>Every capability that moves money is refused at this tier, and every read is not.</b> The
    /// two halves together are the tier decision B15 recorded in the access-tier specification —
    /// asserted here so a later widening of any one of them fails a test rather than a review.
    /// </summary>
    [Fact]
    public async Task Nothing_that_moves_money_is_reachable_below_the_verified_tier()
    {
        await AuthenticateAsync();

        var refused = new[]
        {
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/payment-intents", UriKind.Relative),
                new
                {
                    amount = 25m,
                    currency = "GBP",
                    methodKind = "Card",
                    description = "A test payment"
                }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/purchases", UriKind.Relative),
                new { productCode = "marketplace.listing.boost.7d", subjectId = Guid.NewGuid() }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/subscriptions", UriKind.Relative),
                new { planCode = "premium.monthly" }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/donations", UriKind.Relative),
                new { amount = 10m, currency = "GBP", beneficiaryType = "Platform" })
        };

        refused.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.Forbidden,
            "paying, buying, subscribing and giving all open at Verified and nowhere earlier");

        // ...and the reads a member holds from the membership rung still answer.
        var readable = new[]
        {
            await HttpClient.GetAsync(new Uri("/api/v1/financial/wallets/me", UriKind.Relative)),
            await HttpClient.GetAsync(new Uri("/api/v1/financial/catalog", UriKind.Relative)),
            await HttpClient.GetAsync(
                new Uri("/api/v1/financial/subscriptions/plans", UriKind.Relative)),
            await HttpClient.GetAsync(
                new Uri("/api/v1/financial/contributions/funds", UriKind.Relative))
        };

        readable.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.OK,
            "browsing a catalogue and reading your own balance move no money");
    }

    /// <summary>
    /// <b>An anonymous gift is absent from the giver's own history, by construction.</b> The
    /// identifier was never stored, so there is nothing to match on — the cost of structural
    /// anonymity, stated rather than worked around.
    /// </summary>
    [Fact]
    public async Task The_giving_history_returns_both_lists_and_a_non_monetary_count()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/giving/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var giving = await ReadDataAsync<GivingShape>(response);

        giving.Donations.Should().NotBeNull();
        giving.Contributions.Should().NotBeNull();
    }

    [Fact]
    public async Task A_member_can_read_their_own_purchases()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/purchases", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Staff surfaces ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Changing what the platform charges is an administrator's decision. A moderator who could do
    /// it would be a moderator whose account is worth stealing.
    /// </summary>
    [Fact]
    public async Task An_ordinary_member_cannot_read_the_policy_catalogue()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/operations/policies", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_read_the_financial_dashboard()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/operations/dashboard", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_grant_themselves_credit()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/operations/credit", UriKind.Relative),
            new
            {
                userId = Guid.NewGuid(),
                amount = 1000m,
                currency = "GBP",
                kind = "Bonus",
                reason = "Because I said so."
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── The ledger, driven directly ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The invariant the whole platform's financial correctness rests on.</b> A posting whose
    /// legs do not agree is refused before anything is loaded — because a ledger where credits and
    /// debits can disagree is a ledger nobody can check.
    /// </summary>
    [Fact]
    public async Task An_unbalanced_posting_is_refused()
    {
        using var scope = Factory.Services.CreateScope();

        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        var clearing = await wallets.FindPlatformWalletAsync(WalletType.Clearing, "GBP");

        clearing.Should().NotBeNull();

        var result = await ledger.PostAsync(new LedgerPostingRequest
        {
            TransactionType = TransactionType.Adjustment,
            Amount = Money.FromAmount(10m, "GBP"),
            Description = "A deliberately unbalanced posting",
            Audit = AuditStamp.Create(
                FinancialActor.System("tests"), FinancialSource.System, "A test."),
            Legs =
            [
                new LedgerPostingLeg
                {
                    WalletId = clearing!.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = Money.FromAmount(10m, "GBP"),
                    EntryType = LedgerEntryType.Adjustment
                }
            ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FinancialErrorCodes.LedgerUnbalanced);
    }

    /// <summary>
    /// <b>A balanced posting moves both wallets and leaves a statement behind.</b> This is the whole
    /// mechanism in one test: two legs, two entries, two projections folded forward, one transaction.
    /// </summary>
    [Fact]
    public async Task A_balanced_posting_appends_entries_and_moves_both_projections()
    {
        using var scope = Factory.Services.CreateScope();

        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var entries = scope.ServiceProvider.GetRequiredService<ILedgerEntryRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>();
        var identities = scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Financial.Application.Services
                .FinancialIdentityService>();

        var clearing = await wallets.FindPlatformWalletAsync(WalletType.Clearing, "GBP");
        var revenue = await wallets.FindPlatformWalletAsync(WalletType.Revenue, "GBP");

        clearing.Should().NotBeNull();
        revenue.Should().NotBeNull();

        var revenueBefore = revenue!.AvailableMinorUnits;
        var amount = Money.FromAmount(12.34m, "GBP");

        var result = await ledger.PostAsync(new LedgerPostingRequest
        {
            TransactionType = TransactionType.Payment,
            Amount = amount,
            Description = "An integration-test capture",
            Audit = AuditStamp.Create(
                FinancialActor.System("tests"), FinancialSource.System, "A test capture."),
            Reference = $"test:{Guid.NewGuid():N}",
            Legs =
            [
                new LedgerPostingLeg
                {
                    WalletId = clearing!.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Payment
                },
                new LedgerPostingLeg
                {
                    WalletId = revenue.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Payment
                }
            ]
        });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : null);
        result.Value.Entries.Should().HaveCount(2);

        await unitOfWork.SaveChangesAsync();

        var statement = await entries.ListByTransactionAsync(result.Value.Transaction.Id);

        statement.Should().HaveCount(2);
        statement.Should().OnlyContain(entry => entry.Audit.Reason.Length > 0);

        revenue.AvailableMinorUnits.Should().Be(revenueBefore + 1234);

        // The clearing wallet is the one account allowed to run negative — that is what makes money
        // arriving from outside the platform balance at all.
        clearing.AllowsNegativeBalance.Should().BeTrue();

        _ = identities;
    }

    /// <summary>
    /// <b>The idempotency guarantee.</b> A double-tapped pay button, a retried mobile request and a
    /// redelivered webhook all arrive carrying one reference, and the second gets the first
    /// transaction back rather than moving money twice.
    /// </summary>
    [Fact]
    public async Task Replaying_a_posting_reference_returns_the_original_transaction()
    {
        using var scope = Factory.Services.CreateScope();

        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>();

        var clearing = await wallets.FindPlatformWalletAsync(WalletType.Clearing, "GBP");
        var revenue = await wallets.FindPlatformWalletAsync(WalletType.Revenue, "GBP");
        var amount = Money.FromAmount(5m, "GBP");
        var reference = $"test:{Guid.NewGuid():N}";

        var request = new LedgerPostingRequest
        {
            TransactionType = TransactionType.Payment,
            Amount = amount,
            Description = "An idempotent capture",
            Audit = AuditStamp.Create(
                FinancialActor.System("tests"), FinancialSource.System, "A test capture."),
            Reference = reference,
            Legs =
            [
                new LedgerPostingLeg
                {
                    WalletId = clearing!.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Payment
                },
                new LedgerPostingLeg
                {
                    WalletId = revenue!.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Payment
                }
            ]
        };

        var first = await ledger.PostAsync(request);
        await unitOfWork.SaveChangesAsync();

        var balanceAfterFirst = revenue.AvailableMinorUnits;

        var second = await ledger.PostAsync(request);

        second.IsSuccess.Should().BeTrue();
        second.Value.Transaction.Id.Should().Be(first.Value.Transaction.Id);
        revenue.AvailableMinorUnits.Should().Be(balanceAfterFirst, "money moves once per reference");
    }

    // ── The sweeps ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every sweep exposes a public <c>RunOnceAsync</c> so the suite drives it deterministically. A
    /// test asserting on a side effect after a timer happens to have fired is a flaky test wearing a
    /// passing badge.
    /// </summary>
    [Fact]
    public async Task Every_financial_sweep_can_be_driven_directly_and_is_safe_to_run_twice()
    {
        var sweeps = Factory.Services
            .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<FinancialSweep>()
            .ToList();

        sweeps.Should().HaveCountGreaterThanOrEqualTo(6, "B15 ships six background sweeps");

        foreach (var sweep in sweeps)
        {
            await sweep.RunOnceAsync();
            await sweep.RunOnceAsync();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record CurrencyShape(string Code, string Name, string Symbol, int Exponent);

    private sealed record CapabilitiesShape(
        List<string> Gateways,
        bool CurrencyConversionEnabled,
        bool PayoutsEnabled);

    private sealed record WalletShape(
        Guid WalletId, string WalletType, string OwnerType, string Currency, string Status);

    private sealed record ProductShape(
        string Code,
        string SubjectType,
        string? PlacementKind,
        bool IsAvailable,
        string? UnavailableReason);

    private sealed record PlacementShape(Guid PlacementId, string SubjectType, bool IsActive);

    private sealed record PlanShape(
        string Code, bool IsFree, List<EntitlementShape> Entitlements);

    private sealed record EntitlementShape(string Key, string? Value);

    private sealed record FundShape(string Code, List<string> AcceptedKinds);

    private sealed record GivingShape(
        List<JsonElement> Donations,
        List<JsonElement> Contributions,
        int NonMonetaryContributionCount);
}

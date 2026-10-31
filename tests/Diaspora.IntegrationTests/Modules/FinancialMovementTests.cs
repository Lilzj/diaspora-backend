using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Infrastructure.Gateways;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Money in, money out, and points becoming money — over real HTTP, against a host with a settlement
/// provider registered the way a deployment would register one.
/// <para>
/// <b>The concurrency test is the one this suite exists for.</b> Two withdrawals of the same balance
/// arriving together must leave the wallet consistent, and the barriers that make that true — the
/// posting service's balance check and the wallet's concurrency token — are only exercised by
/// actually racing them.
/// </para>
/// </summary>
public sealed class FinancialMovementTests : IdentityTestBase<FinancialMovementWebApplicationFactory>
{
    /// <summary>
    /// The currency this suite's deposits are made in.
    /// </summary>
    /// <remarks>
    /// Distinct from the member's own wallet currency, which follows their country of residence.
    /// A deposit names its currency and opens a wallet in it, so a member who registers in Canada
    /// and deposits in sterling holds two primary wallets — and a balance assertion has to say
    /// which one it means.
    /// </remarks>
    private const string DepositCurrency = "GBP";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FinancialMovementTests(FinancialMovementWebApplicationFactory factory) : base(factory)
    {
        factory.EnsureDatabaseCreated();
        factory.Settlement.Reset();
        factory.AlternateSettlement.Reset();
        factory.Status.Reset();
        factory.Reconciliation.Reset();
    }

    // ── Deposits ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The gap the frontend called GAP-01, closed.</b> A captured payment used to post
    /// clearing→revenue whatever it was for; a deposit posts clearing→the payer's own wallet, and
    /// the balance a member sees goes up because a ledger entry says so.
    /// </summary>
    [Fact]
    public async Task A_deposit_credits_the_depositors_own_wallet_when_it_captures()
    {
        var userId = await AuthenticateAsync();

        var before = await AvailableAsync(userId);
        var depositId = await CreateDepositAsync(25m);

        await ConfirmByWebhookAsync(depositId, 2500);

        (await AvailableAsync(userId)).Should().Be(before + 2500);
    }

    /// <summary>
    /// A deposit that has been created and not captured moves nothing. The wallet is credited on
    /// capture and never on creation — a member spending money they had only promised is the failure
    /// that makes deposits dangerous.
    /// </summary>
    [Fact]
    public async Task Creating_a_deposit_moves_no_money()
    {
        var userId = await AuthenticateAsync();

        var before = await AvailableAsync(userId);

        await CreateDepositAsync(40m);

        (await AvailableAsync(userId)).Should().Be(before);
    }

    /// <summary>
    /// The limits are the server's. A client that sent an amount below the published minimum is
    /// refused with the code that says which limit it was, rather than a generic validation error.
    /// </summary>
    [Fact]
    public async Task A_deposit_below_the_published_minimum_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative),
            new { amount = 0.50m, currency = "GBP", methodKind = "Card" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.DepositBelowMinimum);
    }

    [Fact]
    public async Task A_deposit_above_the_published_maximum_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative),
            new { amount = 9_999m, currency = "GBP", methodKind = "Card" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.DepositAboveMaximum);
    }

    /// <summary>A negative amount is not a small deposit; it is an attempt to move money the wrong way.</summary>
    [Fact]
    public async Task A_negative_deposit_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative),
            new { amount = -50m, currency = "GBP", methodKind = "Card" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// A double-tapped button is one deposit. The key is uniquely indexed, so this is the database
    /// refusing rather than a handler check a refactor can step around.
    /// </summary>
    [Fact]
    public async Task Two_deposits_carrying_one_idempotency_key_are_one_deposit()
    {
        await AuthenticateAsync();

        var key = Guid.NewGuid().ToString("N");

        var first = await PostDepositAsync(20m, key);
        var second = await PostDepositAsync(20m, key);

        var firstIntent = await ReadDataAsync<PaymentShape>(first);
        var secondIntent = await ReadDataAsync<PaymentShape>(second);

        secondIntent.PaymentIntentId.Should().Be(firstIntent.PaymentIntentId);
    }

    /// <summary>
    /// Deposit history is a view of the payments table filtered by purpose, not a second table — and
    /// an ordinary purchase must not appear in it.
    /// </summary>
    [Fact]
    public async Task Deposit_history_carries_deposits_and_not_purchases()
    {
        await AuthenticateAsync();

        await CreateDepositAsync(15m);

        var payment = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/payment-intents", UriKind.Relative),
            new
            {
                amount = 15m,
                currency = "GBP",
                methodKind = "Card",
                description = "An ordinary payment"
            });

        payment.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative));

        var deposits = await ReadDataAsync<List<PaymentShape>>(response);

        deposits.Should().NotBeEmpty();
        deposits.Should().OnlyContain(deposit => deposit.Purpose == nameof(PaymentPurpose.Deposit));
    }

    // ── Withdrawals ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The hold is what makes a withdrawal safe.</b> Requesting one moves the amount out of
    /// available and into reserved, so the member cannot spend it twice while the payout is in
    /// flight — and the balance a wallet screen shows is the one they can actually use.
    /// </summary>
    [Fact]
    public async Task Requesting_a_withdrawal_holds_the_money_out_of_the_available_balance()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var available = await AvailableAsync(userId);

        var response = await PostWithdrawalAsync(30m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var withdrawal = await ReadDataAsync<WithdrawalShape>(response);

        withdrawal.Status.Should().Be(nameof(WithdrawalStatus.Requested));
        withdrawal.Gross.MinorUnits.Should().Be(3000);
        withdrawal.Net.MinorUnits.Should().Be(3000 - withdrawal.Fee.MinorUnits);

        (await AvailableAsync(userId)).Should().Be(available - 3000);
        (await ReservedAsync(userId)).Should().Be(3000);
    }

    /// <summary>
    /// <b>The test §15 of the brief names.</b> Balance 100, two requests for 80 arriving together;
    /// exactly one may succeed and the wallet must not go negative. Both barriers are load-bearing:
    /// the posting service refuses a debit the balance cannot cover, and the wallet's revision is a
    /// concurrency token that loses one commit when both pass the check against the same read.
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_withdrawals_of_one_balance_leave_the_wallet_consistent()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        var first = PostWithdrawalAsync(80m, destinationId);
        var second = PostWithdrawalAsync(80m, destinationId);

        var responses = await Task.WhenAll(first, second);

        responses.Count(response => response.IsSuccessStatusCode).Should().Be(
            1, "one of two claims on the same money succeeds and one does not");

        var available = await AvailableAsync(userId);
        var reserved = await ReservedAsync(userId);

        available.Should().BeGreaterThanOrEqualTo(0, "a member wallet never goes negative");
        available.Should().Be(10_000 - 8_000);
        reserved.Should().Be(8_000, "exactly one hold exists");
    }

    [Fact]
    public async Task A_withdrawal_larger_than_the_balance_is_refused()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 20m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var response = await PostWithdrawalAsync(50m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.InsufficientFunds);
        (await ReservedAsync(userId)).Should().Be(0, "a refused withdrawal holds nothing");
    }

    /// <summary>
    /// <b>The sweep hands each withdrawal to the provider that serves its corridor, not to whichever
    /// adapter registered first.</b> This is the defect the router was built to remove: the sweep
    /// used to call <c>GetServices&lt;ISettlementProvider&gt;().FirstOrDefault()</c>, which with two
    /// rails configured would send a Kenyan mobile-money payout down a British bank adapter. Two
    /// adapters are registered here precisely so that "the right one" and "the only one" are
    /// different assertions.
    /// </summary>
    [Fact]
    public async Task The_sweep_routes_each_withdrawal_to_the_provider_that_serves_its_corridor()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync(
            countryCode: "KE",
            kind: PayoutDestinationKind.MobileMoney,
            provider: FakeSettlementProvider.AlternateProviderName);

        var created = await PostWithdrawalAsync(10m, destinationId);

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var reference = await ReferenceOf(created);

        await RunSweepAsync<WithdrawalProcessingSweep>();

        // By reference rather than by count: the suite shares one database, so a sweep also picks up
        // whatever other tests left pending. What is under test is which adapter got *this* one.
        Factory.AlternateSettlement.Submitted.Should().Contain(
            instruction => instruction.Reference == reference,
            "the Kenyan mobile-money corridor is served only by the second adapter");

        Factory.Settlement.Submitted.Should().NotContain(
            instruction => instruction.Reference == reference,
            "the first registered adapter serves British bank accounts and nothing else");
    }

    /// <summary>
    /// The other side of the same rule: a British bank payout goes to the adapter whose row covers
    /// it, even though a second adapter is registered and would have answered a blind call.
    /// </summary>
    [Fact]
    public async Task The_sweep_leaves_a_corridor_alone_when_another_adapter_owns_it()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        var created = await PostWithdrawalAsync(10m, destinationId);

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var reference = await ReferenceOf(created);

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Settlement.Submitted.Should().Contain(
            instruction => instruction.Reference == reference);

        Factory.AlternateSettlement.Submitted.Should().NotContain(
            instruction => instruction.Reference == reference);
    }

    /// <summary>
    /// <b>A withdrawal that can no longer be carried is failed and its hold released.</b> Hours pass
    /// between a member asking and a sweep running, and in that window a destination can be removed.
    /// Leaving the request in <c>Requested</c> would hold the money for ever; marking it complete
    /// would say it had been sent. Giving it back is the only honest option, and the member can ask
    /// again once a route exists.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_that_becomes_unroutable_is_failed_and_its_hold_released()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        var created = await PostWithdrawalAsync(10m, destinationId);

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var reference = await ReferenceOf(created);

        (await ReservedAsync(userId)).Should().Be(1_000, "the hold is in place");

        await RemoveDestinationAsync(destinationId);

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Settlement.Submitted.Should().NotContain(
            instruction => instruction.Reference == reference, "there was nowhere to send it");

        Factory.AlternateSettlement.Submitted.Should().NotContain(
            instruction => instruction.Reference == reference);

        (await ReservedAsync(userId)).Should().Be(0, "the hold was released");
        (await AvailableAsync(userId)).Should().Be(10_000, "the money went back to the member");
    }

    /// <summary>
    /// <b>An unknown outcome holds the money. This is the test the whole three-state outcome exists
    /// for.</b> A provider that timed out may already have sent the payment; releasing the hold there
    /// gives the member their balance back while the provider is paying them the same money. The
    /// withdrawal stays in <c>Processing</c> — which is exactly what that status means — and the
    /// reserved balance stays reserved.
    /// </summary>
    [Fact]
    public async Task An_unknown_outcome_holds_the_money_rather_than_giving_it_back()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        var withdrawal = await ReloadAsync(withdrawalId);

        withdrawal.Status.Should().Be(WithdrawalStatus.Processing,
            "the provider was handed the instruction and nobody knows what happened to it");

        (await ReservedAsync(userId)).Should().Be(1_000, "the hold stays");
        (await AvailableAsync(userId)).Should().Be(9_000, "the balance was not given back");
    }

    /// <summary>
    /// The reverse of the rule above, and the reason it is a rule rather than "always hold": a
    /// provider that positively refused moved no money, so the member gets their balance back.
    /// </summary>
    [Fact]
    public async Task A_refusal_still_releases_the_hold()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Accepts = false;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Failed);
        (await ReservedAsync(userId)).Should().Be(0);
        (await AvailableAsync(userId)).Should().Be(10_000, "nothing left, so it all comes back");
    }

    /// <summary>
    /// <b>Holding is only half the answer; something has to find out.</b> Once the grace window has
    /// passed and a status provider says the transfer exists, the withdrawal completes and the hold
    /// becomes a payout.
    /// </summary>
    [Fact]
    public async Task A_held_withdrawal_completes_when_the_provider_says_the_transfer_exists()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Processing);

        // Age it past the grace window, standing in for the time a real sweep would wait.
        await BackdateProcessingAsync(withdrawalId, TimeSpan.FromHours(1));

        Factory.Status.Answers = SettlementOutcome.Accepted("tr_recovered");

        await RunSweepAsync<WithdrawalProcessingSweep>();

        var withdrawal = await ReloadAsync(withdrawalId);

        withdrawal.Status.Should().Be(WithdrawalStatus.Completed);
        Factory.Status.Asked.Should().BeGreaterThan(0);

        (await ReservedAsync(userId)).Should().Be(0, "the hold became a payout");
        (await AvailableAsync(userId)).Should().Be(9_000, "the money left; it did not come back");
    }

    /// <summary>
    /// And the other resolution: the provider holds no such transfer, so nothing left the platform
    /// and the member gets their money back.
    /// </summary>
    [Fact]
    public async Task A_held_withdrawal_fails_and_releases_when_the_provider_has_no_such_transfer()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();
        await BackdateProcessingAsync(withdrawalId, TimeSpan.FromHours(1));

        Factory.Status.Answers = SettlementOutcome.Refused(
            "test.absent", "No transfer exists for it.");

        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Failed);
        (await ReservedAsync(userId)).Should().Be(0);
        (await AvailableAsync(userId)).Should().Be(10_000, "nothing had left");
    }

    /// <summary>
    /// A status provider that still cannot say leaves the withdrawal exactly where it was. Guessing
    /// after a second failed question is no safer than guessing after the first.
    /// </summary>
    [Fact]
    public async Task A_provider_that_still_cannot_say_leaves_the_withdrawal_held()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();
        await BackdateProcessingAsync(withdrawalId, TimeSpan.FromHours(1));

        // Answers stays null, so the fake reports Unresolved.
        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Processing);
        (await ReservedAsync(userId)).Should().Be(1_000);
    }

    /// <summary>
    /// <b>The finding the reconciliation sweep exists for: money the provider says it sent that no
    /// withdrawal accounts for.</b> Nothing else on this path can see it — the status provider only
    /// resolves withdrawals the platform already has a question about, and a webhook only arrives
    /// for a payout the platform started. An orphan is money gone with nothing explaining it, and
    /// the sweep must refuse to invent a withdrawal to explain it: doing so would debit a member's
    /// wallet for a payout they never asked for.
    /// </summary>
    [Fact]
    public async Task An_orphan_payout_is_reported_and_nothing_is_applied()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Reconciliation.Add(new PayoutReconciliationRecord
        {
            Provider = FakeSettlementProvider.ProviderName,
            ProviderReference = "tr_nobody_asked_for",
            Reference = Guid.NewGuid().ToString("N"),
            AmountMinorUnits = 5_000,
            Currency = "GBP",
            Status = "paid",
            OccurredAt = DateTimeOffset.UtcNow
        });

        var examined = await RunSweepAsync<PayoutReconciliationSweep>();

        examined.Should().Be(1, "the record was looked at");

        (await AvailableAsync(userId)).Should().Be(10_000, "no wallet was touched");
        (await ReservedAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// The ordinary case, and the only one that is not a problem: the platform submitted, the
    /// provider confirms, and the sweep closes the gap the delay left.
    /// </summary>
    [Fact]
    public async Task A_payout_the_provider_confirms_is_completed_by_reconciliation()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        // Left in Processing: the provider was handed the instruction and said nothing useful.
        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Processing);

        Factory.Reconciliation.Add(new PayoutReconciliationRecord
        {
            Provider = FakeSettlementProvider.ProviderName,
            ProviderReference = "tr_confirmed",
            Reference = withdrawalId.ToString("N"),

            // The net, which is what the provider was asked to move.
            AmountMinorUnits = 1_000,
            Currency = "GBP",
            Status = "paid",
            OccurredAt = DateTimeOffset.UtcNow
        });

        await RunSweepAsync<PayoutReconciliationSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Completed);
        (await ReservedAsync(userId)).Should().Be(0, "the hold became a payout");
        (await AvailableAsync(userId)).Should().Be(9_000, "the money left");
    }

    /// <summary>
    /// A provider positively saying it failed is the one statement that justifies releasing a hold,
    /// here as everywhere else on this path.
    /// </summary>
    [Fact]
    public async Task A_payout_the_provider_says_failed_is_released_by_reconciliation()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Reconciliation.Add(new PayoutReconciliationRecord
        {
            Provider = FakeSettlementProvider.ProviderName,
            ProviderReference = "tr_failed",
            Reference = withdrawalId.ToString("N"),
            AmountMinorUnits = 1_000,
            Currency = "GBP",
            Status = "failed",
            OccurredAt = DateTimeOffset.UtcNow
        });

        await RunSweepAsync<PayoutReconciliationSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(WithdrawalStatus.Failed);
        (await ReservedAsync(userId)).Should().Be(0);
        (await AvailableAsync(userId)).Should().Be(10_000, "nothing left, so it all comes back");
    }

    /// <summary>
    /// <b>Two contradictory figures about somebody's money are not something a batch job resolves.</b>
    /// Picking either would be the platform choosing which to believe.
    /// </summary>
    [Fact]
    public async Task A_payout_whose_amount_disagrees_is_reported_and_left_alone()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        Factory.Settlement.Unresolved = true;

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);
        var withdrawalId = (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Reconciliation.Add(new PayoutReconciliationRecord
        {
            Provider = FakeSettlementProvider.ProviderName,
            ProviderReference = "tr_wrong_amount",
            Reference = withdrawalId.ToString("N"),
            AmountMinorUnits = 9_999,
            Currency = "GBP",
            Status = "paid",
            OccurredAt = DateTimeOffset.UtcNow
        });

        await RunSweepAsync<PayoutReconciliationSweep>();

        (await ReloadAsync(withdrawalId)).Status.Should().Be(
            WithdrawalStatus.Processing, "a disagreement is not resolved by guessing");

        (await ReservedAsync(userId)).Should().Be(1_000, "the hold stays while it is unresolved");
    }

    /// <summary>
    /// One provider being unreachable must not stop the pass. A reconciliation run that aborted on
    /// the first failure would silently stop checking every other provider.
    /// </summary>
    [Fact]
    public async Task A_provider_that_cannot_be_reached_does_not_stop_the_pass()
    {
        await AuthenticateAsync();

        Factory.Reconciliation.Throws = true;

        var examined = await RunSweepAsync<PayoutReconciliationSweep>();

        examined.Should().Be(0, "nothing was examined, and nothing threw");
    }

    /// <summary>
    /// <b>A saved destination is bound to the provider that verified it, and cannot be paid through
    /// another.</b> The token a destination holds was issued by one provider and is an opaque string
    /// to every other — handing it over would fail at the provider, after the platform had already
    /// decided the money was leaving. The refusal is its own code because the member fixes it by
    /// registering a new destination, not by waiting for their country to be covered.
    /// </summary>
    [Fact]
    public async Task A_destination_cannot_be_paid_through_a_provider_that_did_not_issue_its_token()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        // Kenya is served by the alternate adapter alone, and this destination is bound to the other
        // one. The corridor is reachable; this destination is not the way to reach it.
        var destinationId = await RegisterVerifiedDestinationAsync(
            countryCode: "KE",
            kind: PayoutDestinationKind.MobileMoney,
            provider: FakeSettlementProvider.ProviderName);

        var response = await PostWithdrawalAsync(10m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        (await CodeAsync(response)).Should()
            .Be(FinancialErrorCodes.PayoutDestinationProviderUnavailable);

        (await ReservedAsync(userId)).Should().Be(0, "a refused withdrawal holds nothing");
    }

    /// <summary>
    /// The instruction an adapter receives carries the destination in the provider's own terms. It
    /// used to carry a Diaspora user identifier and nothing else, which no provider can act on.
    /// </summary>
    [Fact]
    public async Task The_settlement_instruction_carries_the_destination_the_provider_will_use()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(10m, destinationId);

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var reference = await ReferenceOf(created);

        await RunSweepAsync<WithdrawalProcessingSweep>();

        var instruction = Factory.Settlement.Submitted
            .Should().ContainSingle(candidate => candidate.Reference == reference).Subject;

        instruction.DestinationReference.Should().Be("tok_verified",
            "the provider identifies the destination by the token it issued");

        instruction.DestinationProvider.Should().Be(FakeSettlementProvider.ProviderName);
        instruction.DestinationKind.Should().Be(PayoutDestinationKind.BankAccount);
        instruction.DestinationCountry.Should().Be("GB");
        instruction.Amount.MinorUnits.Should().Be(1_000, "the provider moves the net");
    }

    /// <summary>
    /// <b>The capability surface reports coverage, not registration.</b> This host has a settlement
    /// provider <em>and</em> a matrix row behind it, so payouts are genuinely available and the
    /// country the money can reach is named. The default host has the provider missing and reports
    /// false — the two together are what stop a client offering a withdrawal screen that refuses
    /// every amount.
    /// </summary>
    [Fact]
    public async Task The_capability_surface_names_the_countries_money_can_actually_reach()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/capabilities", UriKind.Relative));

        var capabilities = await ReadDataAsync<PayoutCapabilitiesShape>(response);

        capabilities.PayoutsEnabled.Should().BeTrue(
            "this host registers a settlement provider and configures a route for it");

        capabilities.PayoutCountries.Should().Equal(
            ["GB", "KE"],
            "both configured corridors have a registered adapter behind them, and the list is sorted");
    }

    /// <summary>
    /// <b>A country the capability matrix does not cover refuses, and holds nothing.</b> This is
    /// the rule the whole router exists for: a registered settlement provider says the deployment
    /// can pay somebody somewhere, never that it can pay this person here. A platform that took the
    /// money out of the balance and then discovered it had nowhere to send it would have created a
    /// hold nobody can release.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_to_a_country_outside_the_matrix_is_refused_and_holds_nothing()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        // Mauritius is not in the harness matrix. The platform's own jurisdiction is not
        // automatically a payout destination, which is the assumption the router refuses to make.
        var destinationId = await RegisterVerifiedDestinationAsync(countryCode: "MU");
        var response = await PostWithdrawalAsync(10m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PayoutCountryUnsupported);
        (await ReservedAsync(userId)).Should().Be(0, "an unroutable payout holds nothing");
        (await AvailableAsync(userId)).Should().Be(10_000, "the balance is untouched");
    }

    /// <summary>
    /// A destination nobody recorded a country for cannot be routed: every capability matrix is
    /// keyed on country, so "somewhere" is not an answer any provider can act on.
    /// </summary>
    [Fact]
    public async Task A_destination_with_no_country_cannot_be_paid_to()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync(countryCode: null);
        var response = await PostWithdrawalAsync(10m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PayoutCountryUnsupported);
        (await ReservedAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// The rail is part of the route. The harness matrix covers a British bank account and says
    /// nothing about mobile money, so the refusal names the rail rather than the country — the
    /// member can act on that by adding a different destination.
    /// </summary>
    [Fact]
    public async Task A_rail_the_matrix_does_not_cover_is_refused_as_a_rail_problem()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync(
            countryCode: "GB", kind: PayoutDestinationKind.MobileMoney);

        var response = await PostWithdrawalAsync(10m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PayoutMethodUnsupported);
        (await ReservedAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// <b>The country is read off the verified destination, never off the request body.</b> A
    /// caller naming a covered country on a withdrawal to an uncovered destination is trying to
    /// pick the rail their money leaves on, and the routing decision must not be theirs to make.
    /// </summary>
    [Fact]
    public async Task A_country_in_the_request_body_cannot_override_the_destinations_own()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync(countryCode: "MU");

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative),
            new
            {
                amount = 10m,
                currency = "GBP",
                destinationId,

                // The country the matrix does cover. It must not help.
                countryCode = "GB",
                idempotencyKey = Guid.NewGuid().ToString("N")
            });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PayoutCountryUnsupported);
        (await ReservedAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// <b>IDOR, on the surface where it would cost the most.</b> A destination identifier belonging
    /// to somebody else answers 404 rather than 403 — a 403 confirms it exists, and a payout
    /// destination is exactly the sort of identifier worth enumerating.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_withdraw_to_somebody_elses_payout_destination()
    {
        await AuthenticateAsync();

        var victimDestination = await RegisterVerifiedDestinationAsync();

        var attackerId = await AuthenticateAsync();

        await FundAsync(attackerId, 100m);

        var response = await PostWithdrawalAsync(10m, victimDestination);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.DestinationNotFound);
    }

    [Fact]
    public async Task A_withdrawal_to_an_unverified_destination_is_refused()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterDestinationAsync();
        var response = await PostWithdrawalAsync(10m, destinationId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.DestinationNotVerified);
    }

    /// <summary>Cancelling releases the hold — the money goes back where it came from.</summary>
    [Fact]
    public async Task Cancelling_a_withdrawal_releases_its_hold()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var created = await PostWithdrawalAsync(30m, destinationId);
        var withdrawal = await ReadDataAsync<WithdrawalShape>(created);

        var response = await HttpClient.PostAsync(
            new Uri(
                $"/api/v1/financial/withdrawals/{withdrawal.WithdrawalId}/cancel", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await AvailableAsync(userId)).Should().Be(10_000);
        (await ReservedAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// <b>The whole payout, end to end.</b> The sweep hands the instruction to the registered
    /// provider, the provider accepts, and the hold becomes a payout: reserved falls to zero and the
    /// money has left the platform.
    /// </summary>
    [Fact]
    public async Task An_accepted_payout_turns_the_hold_into_money_that_has_left()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        await PostWithdrawalAsync(40m, destinationId);

        Factory.Settlement.Accepts = true;

        var handled = await RunSweepAsync<WithdrawalProcessingSweep>();

        handled.Should().BeGreaterThan(0);
        Factory.Settlement.Submitted.Should().NotBeEmpty();

        (await ReservedAsync(userId)).Should().Be(0, "the hold became a payout");
        (await AvailableAsync(userId)).Should().Be(6_000);
    }

    /// <summary>
    /// A provider refusal gives the money back. The safe direction when a payout does not happen is
    /// the member's balance, not a hold nobody will ever release.
    /// </summary>
    [Fact]
    public async Task A_refused_payout_releases_the_hold_back_to_the_member()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        await PostWithdrawalAsync(40m, destinationId);

        Factory.Settlement.Accepts = false;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        (await ReservedAsync(userId)).Should().Be(0);
        (await AvailableAsync(userId)).Should().Be(10_000, "a refused payout returns the money");
    }

    /// <summary>
    /// <b>A provider that throws holds the money. This assertion used to be its opposite, and the
    /// reasoning behind it was wrong.</b> The old test released the hold, on the grounds that "a
    /// member whose funds are stuck is a worse outcome than a reconciliation mismatch with a person
    /// attached". For a payout that trade is inverted: the mismatch <em>is</em> the provider having
    /// sent the money, so releasing the hold pays the member twice and the platform absorbs the
    /// difference. A held balance is recoverable by an operator; a double payment is not.
    /// </summary>
    [Fact]
    public async Task A_settlement_provider_that_throws_holds_the_money()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        await PostWithdrawalAsync(40m, destinationId);

        Factory.Settlement.Throws = true;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Settlement.Throws = false;

        (await ReservedAsync(userId)).Should().Be(4_000, "an exception is not a refusal");
        (await AvailableAsync(userId)).Should().Be(6_000, "the balance was not given back");
    }

    /// <summary>
    /// A double-tapped withdraw button is one withdrawal, refused at the unique index rather than by
    /// a handler check.
    /// </summary>
    [Fact]
    public async Task Two_withdrawals_carrying_one_idempotency_key_are_one_withdrawal()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var key = Guid.NewGuid().ToString("N");

        var first = await PostWithdrawalAsync(20m, destinationId, key);
        var second = await PostWithdrawalAsync(20m, destinationId, key);

        var firstWithdrawal = await ReadDataAsync<WithdrawalShape>(first);
        var secondWithdrawal = await ReadDataAsync<WithdrawalShape>(second);

        secondWithdrawal.WithdrawalId.Should().Be(firstWithdrawal.WithdrawalId);
        (await ReservedAsync(userId)).Should().Be(2_000, "one hold, not two");
    }

    /// <summary>
    /// Two members, one key. The key's index was unique across everyone while the replay lookup was
    /// per member, so the second member found nothing of theirs to replay and then hit the index: a
    /// 500 for an ordinary request. Keys are each member's own now.
    /// </summary>
    [Fact]
    public async Task Two_members_may_use_the_same_withdrawal_idempotency_key()
    {
        var key = "withdraw-" + Guid.NewGuid().ToString("N");

        var first = await AuthenticateAsync();

        await FundAsync(first, 100m);

        var mine = await ReadDataAsync<WithdrawalShape>(
            await PostWithdrawalAsync(20m, await RegisterVerifiedDestinationAsync(), key));

        var second = await AuthenticateAsync();

        await FundAsync(second, 100m);

        var response = await PostWithdrawalAsync(30m, await RegisterVerifiedDestinationAsync(), key);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var theirs = await ReadDataAsync<WithdrawalShape>(response);

        theirs.WithdrawalId.Should().NotBe(mine.WithdrawalId, "the second member withdrew, not replayed the first");
        (await ReservedAsync(second)).Should().Be(3_000);
    }

    /// <summary>A keyless withdrawal had no replay at all; it is refused before anything is held.</summary>
    [Fact]
    public async Task A_withdrawal_without_an_idempotency_key_is_refused()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative),
            new { amount = 20m, currency = "GBP", destinationId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReservedAsync(userId)).Should().Be(0, "nothing was held");
    }

    /// <summary>The header wins over the body, as it does for conversions.</summary>
    [Fact]
    public async Task The_withdrawal_idempotency_key_header_wins_over_the_body()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();
        var headerKey = "header-" + Guid.NewGuid().ToString("N");

        WithdrawalShape? first = null;

        foreach (var bodyKey in new[] { "body-a", "body-b" })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative))
            {
                Content = JsonContent.Create(
                    new { amount = 20m, currency = "GBP", destinationId, idempotencyKey = bodyKey })
            };
            request.Headers.Add("Idempotency-Key", headerKey);

            var withdrawal = await ReadDataAsync<WithdrawalShape>(await HttpClient.SendAsync(request));

            first ??= withdrawal;
            withdrawal.WithdrawalId.Should().Be(first.WithdrawalId, "one header key, one withdrawal");
        }

        (await ReservedAsync(userId)).Should().Be(2_000, "the second request replayed the first");
    }

    /// <summary>
    /// The limits surface serves what the member can actually spend, so a client never has to derive
    /// it from a balance that includes funds a previous withdrawal is already holding.
    /// </summary>
    [Fact]
    public async Task The_withdrawal_limits_surface_reports_the_spendable_balance()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        await PostWithdrawalAsync(30m, destinationId);

        // Names the currency it funded. Unnamed, the limits are quoted in the member's own currency —
        // CAD for this suite's Canadian residents — and this used to pass only because every unnamed
        // read fell back to GBP, the defect fixed on 14 Oct 2026.
        var response = await HttpClient.GetAsync(
            new Uri($"/api/v1/financial/wallets/me/withdrawals/limits?currency={DepositCurrency}", UriKind.Relative));

        var limits = await ReadDataAsync<LimitsShape>(response);

        limits.IsEnabled.Should().BeTrue("a settlement provider is registered on this host");
        limits.Available!.MinorUnits.Should().Be(7_000);
        limits.Minimum.Should().NotBeNull();
    }

    // ── Reward points ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Points are not money until a policy says what they are worth, and the seeded rate is zero —
    /// so the surface is honest about being unavailable rather than pricing them at nothing.
    /// </summary>
    [Fact]
    public async Task Conversion_is_refused_while_no_operator_has_published_a_rate()
    {
        var userId = await AuthenticateAsync();

        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 500L, currency = "GBP", idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(response))
            .Should().Be(FinancialErrorCodes.RewardConversionNotSupported);
    }

    /// <summary>
    /// <b>The one crossing, with a real ledger posting behind it.</b> Points come out of a store that
    /// is not money; the credit comes out of the platform's rewards pot (it was Clearing until CS-A1)
    /// and lands pending until its hold ends (CS-A2).
    /// </summary>
    [Fact]
    public async Task Converting_points_credits_the_wallet_and_posts_to_the_ledger()
    {
        var userId = await AuthenticateAsync();

        var currency = await MemberCurrencyAsync(userId);

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        var before = await PendingAsync(userId, currency);

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 600L, currency, idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var conversion = await ReadDataAsync<ConversionShape>(response);

        conversion.Points.Should().Be(600);
        conversion.Credited.MinorUnits.Should().Be(600);
        conversion.TransactionId.Should().NotBeNull("a conversion is a ledger posting, not a counter");

        (await PendingAsync(userId, currency)).Should().Be(before + 600, "converted credit lands pending (D-7)");
        (await PointsAsync()).Should().Be(400);
    }

    /// <summary>A double-tapped convert button converts once.</summary>
    [Fact]
    public async Task Two_conversions_carrying_one_idempotency_key_convert_once()
    {
        var userId = await AuthenticateAsync();

        var currency = await MemberCurrencyAsync(userId);

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        var key = Guid.NewGuid().ToString("N");

        await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 300L, currency, idempotencyKey = key });

        await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 300L, currency, idempotencyKey = key });

        (await PointsAsync()).Should().Be(700, "points leave once per key");
    }

    [Fact]
    public async Task Converting_more_points_than_the_member_holds_is_refused()
    {
        var userId = await AuthenticateAsync();

        var currency = await MemberCurrencyAsync(userId);

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
        await GrantPointsAsync(userId, 100, "grant:" + Guid.NewGuid().ToString("N"));

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 5_000L, currency, idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PointsAsync()).Should().Be(100, "a refused conversion moves nothing");
    }

    /// <summary>
    /// <b>Reward farming, refused by the grantor's own reference.</b> Two grants describing one
    /// earning award once, whether they arrive as two events or two operator calls.
    /// </summary>
    [Fact]
    public async Task Two_grants_carrying_one_reference_award_points_once()
    {
        var userId = await AuthenticateAsync();

        var reference = "grant:" + Guid.NewGuid().ToString("N");

        await GrantPointsAsync(userId, 250, reference);
        await GrantPointsAsync(userId, 250, reference);

        (await PointsAsync()).Should().Be(250);
    }

    // ── A rate is in one currency, chosen by the server ────────────────────────────────────
    //
    // The rate used to resolve by a country the request named and apply to a currency the request
    // named, so a member could pick both. Each test below restores the seeded state (a zero rate) when
    // it finishes, because the rate is shared by every test in this class.

    [Fact]
    public async Task A_rate_published_in_one_currency_cannot_be_converted_into_another()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);
        var other = currency == "USD" ? "EUR" : "USD";

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, currency = other, idempotencyKey = Guid.NewGuid().ToString("N") });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardCurrencyNotOffered);
            (await PointsAsync()).Should().Be(1_000, "a refused conversion moves nothing");

            // Naming no currency converts in the rate's.
            var converted = await ReadDataAsync<ConversionShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") }));

            converted.Credited.Currency.Should().Be(currency);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// The member's account says GB. The request says CH, where a far better rate is published. The
    /// GB rate applies.
    /// </summary>
    [Fact]
    public async Task The_request_country_does_not_select_the_rate()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await SetAccountCountryAsync(userId, "GB");

            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await PublishConversionRateAsync(minorUnitsPerPoint: 3m, currency, country: "GB");
            await PublishConversionRateAsync(minorUnitsPerPoint: 50m, currency, country: "CH");
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points = 100L, countryCode = "CH" }));

            preview.MinorUnitsPerPoint.Should().Be(3m, "the account's country prices it, not the request's");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>A rate with no currency is a number with no unit, and converts nothing.</summary>
    [Fact]
    public async Task A_rate_without_a_currency_disables_conversion()
    {
        var userId = await AuthenticateAsync();

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency: null);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var terms = await ReadDataAsync<TermsShape>(await HttpClient.GetAsync(
                new Uri("/api/v1/financial/rewards/conversion", UriKind.Relative)));

            terms.IsEnabled.Should().BeFalse();
            terms.DisabledReason.Should().Be(FinancialErrorCodes.RewardConversionNotSupported);
            terms.Currency.Should().Be(
                await MemberCurrencyAsync(userId), "the screen still reads in the member's currency");

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardConversionNotSupported);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// At 0.01 minor units a point, 550 points buy 5 minor units. Five hundred leave the balance and
    /// fifty stay: they would only have bought half a minor unit, and used to vanish with the rest.
    /// </summary>
    [Fact]
    public async Task Points_that_would_buy_a_fraction_of_a_minor_unit_stay_in_the_balance()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 0.01m, currency);
            await GrantPointsAsync(userId, 550, "grant:" + Guid.NewGuid().ToString("N"));

            var before = await PendingAsync(userId, currency);

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points = 550L }));

            preview.Points.Should().Be(550);
            preview.PointsConsumed.Should().Be(500, "the preview says what will actually leave");

            var conversion = await ReadDataAsync<RoundedConversionShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 550L, idempotencyKey = Guid.NewGuid().ToString("N") }));

            conversion.Points.Should().Be(500);
            conversion.PointsRequested.Should().Be(550);
            conversion.Credited.MinorUnits.Should().Be(5);

            (await PointsAsync()).Should().Be(50, "the fifty that bought nothing are still the member's");
            (await PendingAsync(userId, currency)).Should().Be(before + 5, "converted credit lands pending (D-7)");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    // ── The rewards pot: conversions are paid from a funded pot ────────────────────────────
    //
    // Conversions debited Clearing, the one wallet allowed to go negative, so what the platform
    // promised had no ceiling and sat in the deposit float (CNV-02, D-6). They are paid from a pot
    // now, topped up from Revenue, and pause when it runs short.

    [Fact]
    public async Task An_empty_rewards_pot_pauses_conversion_and_moves_nothing()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));
            await SetPlatformBalanceAsync(WalletType.Rewards, currency, 0);

            (await MemberTermsAsync()).Member.Should().Be(new MemberStandingShape(
                false, FinancialErrorCodes.RewardBudgetExhausted, RewardConversionPricer.PausedMessage, null));

            var refused = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            (await CodeAsync(refused)).Should().Be(FinancialErrorCodes.RewardBudgetExhausted);
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>The pot can pay the smallest conversion but not this one: refused, and a smaller one is not.</summary>
    [Fact]
    public async Task A_conversion_larger_than_the_pot_can_pay_is_refused()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));
            await SetPlatformBalanceAsync(WalletType.Rewards, currency, 600);

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points = 700L }));

            preview.IsPermitted.Should().BeFalse();
            preview.RefusalCode.Should().Be(FinancialErrorCodes.RewardBudgetExhausted);

            var smaller = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            smaller.StatusCode.Should().Be(HttpStatusCode.OK, await smaller.Content.ReadAsStringAsync());
            (await PlatformBalanceAsync(WalletType.Rewards, currency)).Should().Be(100);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task A_conversion_is_paid_from_the_pot_and_not_from_clearing()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));
            await SetPlatformBalanceAsync(WalletType.Rewards, currency, 10_000);

            var clearingBefore = await PlatformBalanceAsync(WalletType.Clearing, currency);

            var converted = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            converted.StatusCode.Should().Be(HttpStatusCode.OK, await converted.Content.ReadAsStringAsync());
            (await PlatformBalanceAsync(WalletType.Rewards, currency)).Should().Be(9_500);
            (await PlatformBalanceAsync(WalletType.Clearing, currency)).Should().Be(clearingBefore);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// Staff fill the pot from Revenue and from nothing else, so it never holds more than the
    /// platform has earned (Q1).
    /// </summary>
    [Fact]
    public async Task Staff_top_up_the_pot_from_revenue_and_no_further()
    {
        await SignInAsRoleAsync("Admin");
        await SetPlatformBalanceAsync(WalletType.Revenue, "GBP", 5_000);
        await SetPlatformBalanceAsync(WalletType.Rewards, "GBP", 0);

        var first = await PostTopUpAsync("GBP", 30m, "October rewards budget");

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        (await ReadDataAsync<PotShape>(first)).Available.MinorUnits.Should().Be(3_000);
        (await PlatformBalanceAsync(WalletType.Revenue, "GBP")).Should().Be(2_000);

        var second = await PostTopUpAsync("GBP", 30m, "October rewards budget, again");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(second)).Should().Be(FinancialErrorCodes.InsufficientFunds);
        (await PlatformBalanceAsync(WalletType.Rewards, "GBP")).Should().Be(3_000);

        var pots = await ReadDataAsync<List<PotShape>>(await HttpClient.GetAsync(
            new Uri("/api/v1/financial/operations/rewards/pots", UriKind.Relative)));

        pots.Should().ContainSingle(pot => pot.Currency == "GBP")
            .Which.Available.MinorUnits.Should().Be(3_000);
    }

    [Fact]
    public async Task A_top_up_needs_an_admin_and_a_reason()
    {
        await AuthenticateAsync();

        (await PostTopUpAsync("GBP", 1m, "A member trying")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Admin");

        (await PostTopUpAsync("GBP", 1m, " ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Staff grants: a reason, and the reward.grant policy's limits ────────────────────────
    //
    // The one route that creates points with no earning behind them had no upper bound and asked
    // nothing about why. These run against the shipped policy: 1,000 points a grant, 5,000 a day for
    // one staff member, and a second Admin's approval for anything above 500 (RWD-10).

    /// <summary>A grant must say why; a blank reason is refused before anything is awarded.</summary>
    [Fact]
    public async Task A_staff_grant_without_a_reason_is_refused()
    {
        var member = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");

        var response = await PostGrantAsync(member, 100, "grant:" + Guid.NewGuid().ToString("N"), reason: "  ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await BalanceOfAsync(member)).Should().Be(0);
    }

    [Fact]
    public async Task A_staff_grant_above_the_per_grant_maximum_is_refused()
    {
        var member = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");

        var response = await PostGrantAsync(member, 1_001, "grant:" + Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardGrantAboveMaximum);
        (await BalanceOfAsync(member)).Should().Be(0);
    }

    /// <summary>
    /// The ceiling is the grantor's, not the member's: one staff account is what it bounds. A retry
    /// of a grant that already landed still replays at the ceiling, and another staff member can
    /// still grant.
    /// </summary>
    [Fact]
    public async Task One_staff_member_cannot_grant_past_the_daily_maximum()
    {
        var member = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");

        // 500 a grant, the most one Admin may grant alone.
        var references = Enumerable.Range(0, 10).Select(_ => "grant:" + Guid.NewGuid().ToString("N")).ToList();

        foreach (var reference in references)
        {
            (await PostGrantAsync(member, 500, reference)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await PostGrantAsync(member, 500, references[^1])).StatusCode.Should().Be(
            HttpStatusCode.OK, "a retried grant replays; it is not an eleventh grant");

        var over = await PostGrantAsync(member, 1, "grant:" + Guid.NewGuid().ToString("N"));

        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(over)).Should().Be(FinancialErrorCodes.RewardGrantDailyLimitReached);
        (await PendingPointsOfAsync(member)).Should().Be(5_000, "a staff grant vests like any other (D-9)");

        await SignInAsRoleAsync("Admin");

        (await PostGrantAsync(member, 1, "grant:" + Guid.NewGuid().ToString("N"))).StatusCode
            .Should().Be(HttpStatusCode.OK, "the ceiling is per grantor");
    }

    [Fact]
    public async Task A_staff_grant_records_who_granted_it_and_why()
    {
        var member = await AuthenticateAsync();

        var (admin, _) = await SignInAsRoleAsync("Admin");

        (await PostGrantAsync(member, 100, "grant:" + Guid.NewGuid().ToString("N"), "Missed referral, ticket 4411"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(member);

        var entry = account!.Entries.Should().ContainSingle().Subject;

        entry.GrantedByUserId.Should().Be(admin);
        entry.Reason.Should().Be("Missed referral, ticket 4411");
        entry.PolicyVersion.Should().MatchRegex(@"^reward\.grant@\d+$", "the limits that allowed it are on record");
        entry.ApprovedByUserId.Should().BeNull("a grant at or below the threshold needs one hand");
    }

    // ── Staff grants: a second Admin above the threshold (RWD-10) ───────────────────────────
    //
    // The per-grant and daily limits bound one staff account's hand; above 500 points the grant also
    // needs somebody else's. The grant route refuses it and points at a request, which a different
    // Admin approves. The Admin who asked may withdraw it, and nobody grants points to themselves.

    [Fact]
    public async Task A_staff_grant_above_the_approval_threshold_is_refused_and_moves_nothing()
    {
        var member = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");

        var response = await PostGrantAsync(member, 501, "grant:" + Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardGrantNeedsApproval);
        (await PendingPointsOfAsync(member)).Should().Be(0);
        (await BalanceOfAsync(member)).Should().Be(0);
    }

    [Fact]
    public async Task A_grant_request_lands_once_when_a_second_admin_approves_it()
    {
        var member = await AuthenticateAsync();
        var (asker, askerAuth) = await SignInAsRoleAsync("Admin");
        var (approver, approverAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);
        var requested = await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N"), "Event prize, ticket 5120");
        requested.StatusCode.Should().Be(HttpStatusCode.OK, await requested.Content.ReadAsStringAsync());
        var requestId = await RequestIdAsync(requested);
        (await StatusOfAsync(requested)).Should().Be("Pending");
        (await PendingPointsOfAsync(member)).Should().Be(0, "nothing lands on the asking");

        ActAs(approverAuth);
        var approved = await DecideGrantRequestAsync(requestId, approve: true, note: null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await StatusOfAsync(approved)).Should().Be("Approved");

        (await PendingPointsOfAsync(member)).Should().Be(800, "an approved grant vests like any other (D-9)");

        using (var scope = Factory.Services.CreateScope())
        {
            var account = await scope.ServiceProvider
                .GetRequiredService<IRewardAccountRepository>()
                .FindByMemberAsync(member);

            var entry = account!.Entries.Should().ContainSingle().Subject;

            entry.GrantedByUserId.Should().Be(asker);
            entry.ApprovedByUserId.Should().Be(approver);
            entry.Reason.Should().Be("Event prize, ticket 5120");
            entry.PolicyVersion.Should().MatchRegex(@"^reward\.grant@\d+$");
        }

        var again = await DecideGrantRequestAsync(requestId, approve: true, note: null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(again)).Should().Be(FinancialErrorCodes.RewardGrantRequestDecided);
        (await PendingPointsOfAsync(member)).Should().Be(800, "a request lands once");
    }

    [Fact]
    public async Task The_admin_who_asked_cannot_approve_their_own_request()
    {
        var member = await AuthenticateAsync();
        await SignInAsRoleAsync("Admin");

        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N")));

        var approved = await DecideGrantRequestAsync(requestId, approve: true, note: null);

        approved.StatusCode.Should().Be(HttpStatusCode.Forbidden, await approved.Content.ReadAsStringAsync());
        (await CodeAsync(approved)).Should().Be(FinancialErrorCodes.RewardGrantSelfApproval);
        (await PendingPointsOfAsync(member)).Should().Be(0);
    }

    /// <summary>With one Admin on a deployment, nobody else could ever close the request.</summary>
    [Fact]
    public async Task The_admin_who_asked_can_withdraw_the_request()
    {
        var member = await AuthenticateAsync();
        var (_, askerAuth) = await SignInAsRoleAsync("Admin");
        var (_, otherAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);
        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N")));

        var withdrawn = await DecideGrantRequestAsync(requestId, approve: false, note: "Raised against the wrong member.");
        withdrawn.StatusCode.Should().Be(HttpStatusCode.OK, await withdrawn.Content.ReadAsStringAsync());
        (await StatusOfAsync(withdrawn)).Should().Be("Withdrawn");

        ActAs(otherAuth);
        (await DecideGrantRequestAsync(requestId, approve: true, note: null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a withdrawn request is decided");
        (await PendingPointsOfAsync(member)).Should().Be(0);
    }

    [Fact]
    public async Task Declining_a_request_needs_a_note()
    {
        var member = await AuthenticateAsync();
        var (_, askerAuth) = await SignInAsRoleAsync("Admin");
        var (_, otherAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);
        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N")));

        ActAs(otherAuth);
        (await DecideGrantRequestAsync(requestId, approve: false, note: " ")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var rejected = await DecideGrantRequestAsync(requestId, approve: false, note: "No ticket behind it.");
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(rejected)).Should().Be("Rejected");
    }

    /// <summary>
    /// The grant lands when it is approved, so the asker's day is measured then, not when they
    /// asked: an approval cannot carry them past the ceiling.
    /// </summary>
    [Fact]
    public async Task An_approval_rechecks_the_askers_daily_ceiling()
    {
        var member = await AuthenticateAsync();
        var (_, askerAuth) = await SignInAsRoleAsync("Admin");
        var (_, approverAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);
        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N")));

        for (var i = 0; i < 9; i++)
        {
            (await PostGrantAsync(member, 500, "grant:" + Guid.NewGuid().ToString("N"))).StatusCode
                .Should().Be(HttpStatusCode.OK);
        }

        ActAs(approverAuth);
        var approved = await DecideGrantRequestAsync(requestId, approve: true, note: null);

        approved.StatusCode.Should().Be(HttpStatusCode.Conflict, await approved.Content.ReadAsStringAsync());
        (await CodeAsync(approved)).Should().Be(FinancialErrorCodes.RewardGrantDailyLimitReached);
        (await PendingPointsOfAsync(member)).Should().Be(4_500);
    }

    [Fact]
    public async Task A_request_is_still_bounded_by_the_per_grant_maximum()
    {
        var member = await AuthenticateAsync();
        await SignInAsRoleAsync("Admin");

        var response = await PostGrantRequestAsync(member, 1_001, "grant:" + Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardGrantAboveMaximum);
    }

    [Fact]
    public async Task A_repeated_request_reference_replays_the_request()
    {
        var member = await AuthenticateAsync();
        await SignInAsRoleAsync("Admin");
        var reference = "grant:" + Guid.NewGuid().ToString("N");

        var first = await RequestIdAsync(await PostGrantRequestAsync(member, 800, reference));
        var second = await RequestIdAsync(await PostGrantRequestAsync(member, 800, reference));

        second.Should().Be(first);
    }

    /// <summary>Q4: nobody grants points to their own account, at any size, by either route.</summary>
    [Fact]
    public async Task Nobody_grants_points_to_themselves()
    {
        var member = await AuthenticateAsync();
        var (asker, askerAuth) = await SignInAsRoleAsync("Admin");
        var (approver, approverAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);

        var direct = await PostGrantAsync(asker, 10, "grant:" + Guid.NewGuid().ToString("N"));
        direct.StatusCode.Should().Be(HttpStatusCode.Forbidden, await direct.Content.ReadAsStringAsync());
        (await CodeAsync(direct)).Should().Be(FinancialErrorCodes.RewardGrantToSelf);

        var request = await PostGrantRequestAsync(asker, 800, "grant:" + Guid.NewGuid().ToString("N"));
        request.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(request)).Should().Be(FinancialErrorCodes.RewardGrantToSelf);

        // Asked for somebody else, approved by the person it pays.
        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(approver, 800, "grant:" + Guid.NewGuid().ToString("N")));

        ActAs(approverAuth);
        var approved = await DecideGrantRequestAsync(requestId, approve: true, note: null);
        approved.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(approved)).Should().Be(FinancialErrorCodes.RewardGrantToSelf);

        (await PendingPointsOfAsync(asker)).Should().Be(0);
        (await PendingPointsOfAsync(approver)).Should().Be(0);
        (await PendingPointsOfAsync(member)).Should().Be(0);
    }

    [Fact]
    public async Task Staff_see_pending_requests_in_the_queue()
    {
        var member = await AuthenticateAsync();
        await SignInAsRoleAsync("Admin");

        var requestId = await RequestIdAsync(
            await PostGrantRequestAsync(member, 800, "grant:" + Guid.NewGuid().ToString("N")));

        var queue = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/operations/rewards/grant-requests?take=200", UriKind.Relative));

        queue.StatusCode.Should().Be(HttpStatusCode.OK, await queue.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());

        body.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("requestId").GetGuid())
            .Should().Contain(requestId);
    }

    /// <summary>
    /// A grantor's rule version travels on its event and is kept on the entry it produces, so the
    /// earning can still be explained after the rule changes.
    /// </summary>
    [Fact]
    public async Task A_granted_events_policy_version_is_kept_on_its_entry()
    {
        var member = await AuthenticateAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var consumer = ActivatorUtilities.CreateInstance<
                Diaspora.Modules.Financial.Application.Consumers.Rewards.RewardPointsGrantedConsumer>(
                scope.ServiceProvider);

            await consumer.Handle(
                new IntegrationEventNotification<RewardPointsGrantedIntegrationEvent>(
                    new RewardPointsGrantedIntegrationEvent
                    {
                        MemberUserId = member,
                        Points = 250,
                        Source = "ReferralQualified",
                        GrantReference = "referral:" + Guid.NewGuid().ToString("N"),
                        GrantedAt = DateTimeOffset.UtcNow,
                        PolicyVersion = "creator.referral@2"
                    }),
                CancellationToken.None);
        }

        using var read = Factory.Services.CreateScope();

        var account = await read.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(member);

        account!.Entries.Should().ContainSingle().Which.PolicyVersion.Should().Be("creator.referral@2");
    }

    // ── Points vesting (D-9, RWD-07) ─────────────────────────────────────────────────────
    //
    // A grant was spendable the moment it landed, so a point earned for a guide could be converted
    // before the guide was deleted. Grants are pending for a week now (Q3: pending points can be
    // neither converted nor spent), and a sweep vests them when the week ends.

    [Fact]
    public async Task A_granted_point_is_pending_and_cannot_be_converted_until_it_vests()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await DeliverGrantAsync(userId, 1_000, DateTimeOffset.UtcNow);

            var balance = await RewardBalanceAsync();
            balance.Balance.Should().Be(0);
            balance.PendingPoints.Should().Be(1_000);
            balance.LifetimeEarned.Should().Be(1_000);
            balance.NextVestsAt.Should().BeCloseTo(
                DateTimeOffset.UtcNow.AddDays(RewardVesting.DefaultVestingDays), TimeSpan.FromMinutes(5));

            var conversion = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            conversion.StatusCode.Should().Be(HttpStatusCode.Conflict, "pending points are not convertible");

            await RunSweepAsync<RewardVestingSweep>();

            (await RewardBalanceAsync()).PendingPoints.Should().Be(1_000, "nothing is due for a week");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task The_vesting_sweep_vests_a_due_grant_once_and_it_can_then_be_converted()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);

            // Granted eight days ago, so its week ended yesterday.
            await DeliverGrantAsync(userId, 1_000, DateTimeOffset.UtcNow.AddDays(-8));

            (await RewardBalanceAsync()).PendingPoints.Should().Be(1_000);

            await RunSweepAsync<RewardVestingSweep>();
            await RunSweepAsync<RewardVestingSweep>();

            var balance = await RewardBalanceAsync();
            balance.Balance.Should().Be(1_000, "a grant vests once, however often the sweep runs");
            balance.PendingPoints.Should().Be(0);
            balance.NextVestsAt.Should().BeNull();

            (await HttpClient.PostAsJsonAsync(
                    new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                    new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") }))
                .IsSuccessStatusCode.Should().BeTrue();
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>A hold placed during the window keeps the points pending, and reversible, until it lifts.</summary>
    [Fact]
    public async Task A_held_members_points_stay_pending_until_the_hold_lifts()
    {
        var userId = await AuthenticateAsync();

        await DeliverGrantAsync(userId, 300, DateTimeOffset.UtcNow.AddDays(-8));
        await SetConversionHoldAsync(userId, held: true);

        await RunSweepAsync<RewardVestingSweep>();

        (await PendingPointsOfAsync(userId)).Should().Be(300);

        await SetConversionHoldAsync(userId, held: false);
        await RunSweepAsync<RewardVestingSweep>();

        (await PendingPointsOfAsync(userId)).Should().Be(0);
        (await BalanceOfAsync(userId)).Should().Be(300);
    }

    /// <summary>The window is the policy's: zero days means spendable at once.</summary>
    [Fact]
    public async Task A_policy_with_no_vesting_window_grants_spendable_points()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency, vestingDays: 0);
            await DeliverGrantAsync(userId, 400, DateTimeOffset.UtcNow);

            (await BalanceOfAsync(userId)).Should().Be(400);
            (await PendingPointsOfAsync(userId)).Should().Be(0);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    // ── Reversing pending points (D-9, RWD-08) ───────────────────────────────────────────
    //
    // Nothing could take points back. A grantor withdraws a grant now, and the Financial Platform
    // takes back what is still pending and leaves what has vested.

    [Fact]
    public async Task A_withdrawn_pending_grant_is_taken_back_once()
    {
        var userId = await AuthenticateAsync();
        var reference = "guide:" + Guid.NewGuid().ToString("N");

        await DeliverGrantAsync(userId, 1_000, DateTimeOffset.UtcNow, reference);

        await DeliverReversalAsync(userId, reference, "Guide deleted.");
        await DeliverReversalAsync(userId, reference, "Guide deleted.");

        var balance = await RewardBalanceAsync();
        balance.PendingPoints.Should().Be(0);
        balance.Balance.Should().Be(0);
        balance.LifetimeReversed.Should().Be(1_000, "a redelivered withdrawal takes the points back once");

        var account = await RewardAccountOfAsync(userId);
        account.Entries.Should().ContainSingle(entry => entry.IsReversal).Which.Points.Should().Be(1_000);
        account.Entries.Single(entry => entry.Reference == reference).ReversalReason.Should().Be("Guide deleted.");
    }

    /// <summary>D-9: points that could have been spent stay earned, and the request is on the record.</summary>
    [Fact]
    public async Task A_withdrawn_grant_that_has_vested_stays_earned()
    {
        var userId = await AuthenticateAsync();
        var reference = "guide:" + Guid.NewGuid().ToString("N");

        await DeliverGrantAsync(userId, 1_000, DateTimeOffset.UtcNow.AddDays(-8), reference);
        await RunSweepAsync<RewardVestingSweep>();

        await DeliverReversalAsync(userId, reference, "Guide deleted.");

        (await BalanceOfAsync(userId)).Should().Be(1_000);

        var grant = (await RewardAccountOfAsync(userId)).Entries.Single(entry => entry.Reference == reference);
        grant.ReversalDeclinedAt.Should().NotBeNull();
        grant.ReversedAt.Should().BeNull();
    }

    /// <summary>A grant taken back before its window ended is not vested when the window does.</summary>
    [Fact]
    public async Task A_withdrawn_grant_is_never_vested_by_the_sweep()
    {
        var userId = await AuthenticateAsync();
        var reference = "guide:" + Guid.NewGuid().ToString("N");

        await DeliverGrantAsync(userId, 1_000, DateTimeOffset.UtcNow.AddDays(-8), reference);
        await DeliverReversalAsync(userId, reference, "Guide deleted.");

        await RunSweepAsync<RewardVestingSweep>();

        (await BalanceOfAsync(userId)).Should().Be(0);
        (await PendingPointsOfAsync(userId)).Should().Be(0);
    }

    // ── The hold on converted credit (D-7) ───────────────────────────────────────────────
    //
    // Converted credit landed in the withdrawable primary wallet at once (CNV-07). It lands pending
    // now, and a sweep makes it available when the hold ends: 14 days unless the policy says
    // otherwise.

    [Fact]
    public async Task Converted_credit_is_held_before_it_can_be_spent_or_withdrawn()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var before = (await PrimaryWalletAsync(userId, currency))!;

            var conversion = await ReadDataAsync<ConversionShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") }));

            conversion.Status.Should().Be("Holding");
            conversion.AvailableAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromMinutes(5));

            await RunSweepAsync<RewardConversionReleaseSweep>();

            var after = (await PrimaryWalletAsync(userId, currency))!;

            after.PendingMinorUnits.Should().Be(before.PendingMinorUnits + 500, "the credit waits out its hold");
            after.AvailableMinorUnits.Should().Be(before.AvailableMinorUnits, "nothing can be spent or withdrawn yet");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>When the hold ends the sweep releases the credit, once.</summary>
    [Fact]
    public async Task The_release_sweep_makes_held_credit_available_when_the_hold_ends()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency, holdDays: 0);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var before = (await PrimaryWalletAsync(userId, currency))!;

            var conversion = await ReadDataAsync<ConversionShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") }));

            conversion.Status.Should().Be("Holding");

            await RunSweepAsync<RewardConversionReleaseSweep>();
            await RunSweepAsync<RewardConversionReleaseSweep>();

            var after = (await PrimaryWalletAsync(userId, currency))!;

            after.AvailableMinorUnits.Should().Be(before.AvailableMinorUnits + 500, "released once, not twice");
            after.PendingMinorUnits.Should().Be(before.PendingMinorUnits);

            var history = await ReadDataAsync<List<ConversionShape>>(await HttpClient.GetAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative)));

            history.Single(row => row.ConversionId == conversion.ConversionId).Status.Should().Be("Available");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// A member held during the window keeps their credit pending until the hold is lifted; this is
    /// what gives a review time before money can leave.
    /// </summary>
    [Fact]
    public async Task A_held_members_converted_credit_stays_pending_until_the_hold_lifts()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency, holdDays: 0);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var before = (await PrimaryWalletAsync(userId, currency))!;

            (await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            await SetConversionHoldAsync(userId, held: true);
            await RunSweepAsync<RewardConversionReleaseSweep>();

            (await PrimaryWalletAsync(userId, currency))!.PendingMinorUnits
                .Should().Be(before.PendingMinorUnits + 500, "a held member's credit is not released");

            await SetConversionHoldAsync(userId, held: false);
            await RunSweepAsync<RewardConversionReleaseSweep>();

            (await PrimaryWalletAsync(userId, currency))!.AvailableMinorUnits
                .Should().Be(before.AvailableMinorUnits + 500);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    // ── Staff conversion holds: placed, lifted, and kept ───────────────────────────────────
    //
    // The account could be held and conversion honoured it, but nothing could place a hold. The
    // platform's audit service writes a log line, so each decision is kept on the account.

    /// <summary>
    /// A staff hold stops conversion, and the member is told only that it is held: the operator's
    /// note never reaches them.
    /// </summary>
    [Fact]
    public async Task A_staff_hold_stops_conversion_and_the_member_sees_only_that_it_is_held()
    {
        var member = await AuthenticateAsync();
        var memberToken = HttpClient.DefaultRequestHeaders.Authorization!.Parameter!;
        var currency = await MemberCurrencyAsync(member);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(member, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            await SignInAsRoleAsync("Admin");

            var held = await ReadDataAsync<HoldShape>(await PutHoldAsync(member, true, "Referral ring, case 77"));

            held.IsHeld.Should().BeTrue();
            held.Reason.Should().Be("Referral ring, case 77");

            UseToken(memberToken);

            (await MemberTermsAsync()).Member.Should().Be(new MemberStandingShape(
                false, FinancialErrorCodes.RewardNotEligible, RewardConversionPricer.HeldMessage, null));

            var refused = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            (await CodeAsync(refused)).Should().Be(FinancialErrorCodes.RewardNotEligible);
            (await refused.Content.ReadAsStringAsync()).Should().NotContain("case 77");
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>Lifting the hold lets the member convert, and both decisions are kept with who and why.</summary>
    [Fact]
    public async Task Lifting_a_staff_hold_lets_the_member_convert_and_both_decisions_are_kept()
    {
        var member = await AuthenticateAsync();
        var memberToken = HttpClient.DefaultRequestHeaders.Authorization!.Parameter!;
        var currency = await MemberCurrencyAsync(member);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(member, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var (admin, _) = await SignInAsRoleAsync("Admin");

            await ReadDataAsync<HoldShape>(await PutHoldAsync(member, true, "Under review"));

            var lifted = await ReadDataAsync<HoldShape>(await PutHoldAsync(member, false, "Review cleared"));

            lifted.IsHeld.Should().BeFalse();
            lifted.Reason.Should().BeNull();
            lifted.Decisions.Select(decision => (decision.IsHeld, decision.Reason, decision.DecidedByUserId))
                .Should().Equal((false, "Review cleared", admin), (true, "Under review", admin));

            UseToken(memberToken);

            var converted = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            converted.StatusCode.Should().Be(HttpStatusCode.OK, await converted.Content.ReadAsStringAsync());
            (await PointsAsync()).Should().Be(500);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task A_staff_hold_without_a_reason_is_refused()
    {
        var member = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");

        var response = await PutHoldAsync(member, true, reason: " ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());

        using var scope = Factory.Services.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>().FindStandingAsync(member))
            .Should().BeNull("nothing was held, so no account was opened to hold");
    }

    /// <summary>Lifting a hold that is not in force is not a decision, and nothing is recorded.</summary>
    [Fact]
    public async Task Lifting_a_hold_that_is_not_in_force_records_nothing()
    {
        var member = await AuthenticateAsync();

        await GrantPointsAsync(member, 100, "grant:" + Guid.NewGuid().ToString("N"));
        await SignInAsRoleAsync("Admin");

        var state = await ReadDataAsync<HoldShape>(await PutHoldAsync(member, false, "Nothing to lift"));

        state.IsHeld.Should().BeFalse();
        state.Decisions.Should().BeEmpty();
    }

    // ── The review queue: every held account, with the decision in force (CS-D4) ────────────
    //
    // Staff could hold and lift a member they already knew about, and nothing listed who was held.
    // A system hold has no staff member behind it, so without a list nobody would find it to lift.

    [Fact]
    public async Task Staff_see_every_held_account_newest_decision_first_with_the_note_in_force()
    {
        var bySystem = await AuthenticateAsync();
        var lifted = await AuthenticateAsync();
        var byStaff = await AuthenticateAsync();
        await GrantPointsAsync(byStaff, 300, "grant:" + Guid.NewGuid().ToString("N"));
        await GrantPointsAsync(bySystem, 100, "grant:" + Guid.NewGuid().ToString("N"));

        await SetConversionHoldAsync(bySystem, held: true);

        var (admin, _) = await SignInAsRoleAsync("Admin");

        await ReadDataAsync<HoldShape>(await PutHoldAsync(lifted, true, "Under review"));
        await ReadDataAsync<HoldShape>(await PutHoldAsync(lifted, false, "Review cleared"));
        await ReadDataAsync<HoldShape>(await PutHoldAsync(byStaff, true, "Referral ring, case 77"));
        await ReadDataAsync<HoldShape>(await PutHoldAsync(byStaff, true, "Referral ring, case 78"));

        var queue = await ReadDataAsync<List<HeldAccountShape>>(await GetHoldsAsync());

        queue.Select(row => row.UserId).Should().NotContain(lifted, "a lifted hold leaves the queue");
        queue.Take(2).Select(row => row.UserId).Should().Equal(byStaff, bySystem);

        queue[0].Balance.Should().Be(300);
        queue[0].PendingBalance.Should().Be(0);
        queue[0].LatestDecision.Should().BeEquivalentTo(
            new { IsHeld = true, Reason = "Referral ring, case 78", DecidedByUserId = (Guid?)admin },
            "the note in force is the latest hold's");

        queue[1].LatestDecision.DecidedByUserId.Should().BeNull("the platform held this one itself");
        queue[1].LatestDecision.Reason.Should().Be("Integration suite: under review.");
    }

    [Fact]
    public async Task The_queue_pages_by_the_decisions_instant_and_the_members_already_shown()
    {
        var first = await AuthenticateAsync();
        var second = await AuthenticateAsync();

        await SignInAsRoleAsync("Admin");
        await ReadDataAsync<HoldShape>(await PutHoldAsync(first, true, "Under review"));
        await ReadDataAsync<HoldShape>(await PutHoldAsync(second, true, "Under review"));

        var page = await ReadDataAsync<List<HeldAccountShape>>(await GetHoldsAsync(take: 1));

        page.Should().ContainSingle().Which.UserId.Should().Be(second);

        var next = await ReadDataAsync<List<HeldAccountShape>>(
            await GetHoldsAsync(before: page[0].LatestDecision.DecidedAt, excludeIds: [second], take: 1));

        next.Should().ContainSingle().Which.UserId.Should().Be(first);
    }

    [Fact]
    public async Task A_member_cannot_read_the_queue()
    {
        await AuthenticateAsync();

        (await GetHoldsAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── The idempotency key: required, and each member's own ────────────────────────────────

    /// <summary>
    /// Two members, one key. The key's index was unique across everyone while the replay lookup was
    /// per member, so the second member found no replay of their own and then hit the index: a 500
    /// for an ordinary request. Keys are each member's own now.
    /// </summary>
    [Fact]
    public async Task Two_members_may_use_the_same_idempotency_key()
    {
        var key = "convert-" + Guid.NewGuid().ToString("N");

        var first = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(first);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(first, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var mine = await ReadDataAsync<ConversionShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 300L, idempotencyKey = key }));

            var second = await AuthenticateAsync();
            (await MemberCurrencyAsync(second)).Should().Be(currency, "both members convert at the one published rate");
            await GrantPointsAsync(second, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 200L, idempotencyKey = key });

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

            var theirs = await ReadDataAsync<ConversionShape>(response);

            theirs.ConversionId.Should().NotBe(mine.ConversionId, "the second member converted, not replayed the first");
            theirs.Points.Should().Be(200);
            (await PointsAsync()).Should().Be(800);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>A keyless conversion had no replay at all; it is refused before anything moves.</summary>
    [Fact]
    public async Task A_conversion_without_an_idempotency_key_is_refused()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 300L });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>The header wins over the body, as it does in the Creator and Programme modules.</summary>
    [Fact]
    public async Task The_idempotency_key_header_wins_over_the_body()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);
        var headerKey = "header-" + Guid.NewGuid().ToString("N");

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            ConversionShape? first = null;

            foreach (var bodyKey in new[] { "body-a", "body-b" })
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative))
                {
                    Content = JsonContent.Create(new { points = 300L, idempotencyKey = bodyKey })
                };
                request.Headers.Add("Idempotency-Key", headerKey);

                var conversion = await ReadDataAsync<ConversionShape>(await HttpClient.SendAsync(request));

                first ??= conversion;
                conversion.ConversionId.Should().Be(first.ConversionId, "one header key, one conversion");
            }

            (await PointsAsync()).Should().Be(700, "the second request replayed the first");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    // ── Who may convert: the bar withdrawals already set ───────────────────────────────────
    //
    // Converted credit lands in the withdrawable primary wallet, so converting is held to the rule
    // for moving money out: an Active financial account. Conversion checked nothing about the account
    // before, so an unverified or restricted member could mint withdrawable credit.

    /// <summary>
    /// A member Identity has verified, whose financial account was never activated — the approval
    /// event is still queued (or was lost). A member who is not verified at all never reaches this
    /// route: the tier check refuses them first (<c>FinancialContractGapTests</c>). This is the case
    /// that check cannot see, and the account gate is what stops it.
    /// </summary>
    [Fact]
    public async Task A_verified_member_whose_financial_account_was_never_activated_cannot_convert()
    {
        // The outbox is deliberately not drained: the events that would open and activate this
        // member's financial account have not been consumed.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        var userId = auth.User.Id;
        const string currency = DepositCurrency;

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points = 500L }));

            preview.IsPermitted.Should().BeFalse();
            preview.RefusalCode.Should().Be(FinancialErrorCodes.AccountNotEligible, "the preview says why first");

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "as a withdrawal from this account would be");
            (await CodeAsync(response)).Should().Be(FinancialErrorCodes.AccountNotEligible);
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task A_restricted_account_cannot_convert()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            using (var scope = Factory.Services.CreateScope())
            {
                var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
                var account = (await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal))!;

                account.Restrict("Identity verification was revoked.", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
                accounts.Update(account);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 500L, idempotencyKey = Guid.NewGuid().ToString("N") });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodeAsync(response)).Should().Be(FinancialErrorCodes.AccountRestricted);
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// A hold on the points account used to surface only when the conversion was attempted, as a bare
    /// conflict. The preview now says it is held — and only that: the hold's own reason is an
    /// operator's note, and naming the check a refusal tripped is a map for getting round it.
    /// </summary>
    [Fact]
    public async Task A_held_points_account_is_told_it_is_held_in_the_preview()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            using (var scope = Factory.Services.CreateScope())
            {
                var rewards = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
                var account = (await rewards.FindByMemberAsync(userId))!;

                account.BlockConversion(Guid.NewGuid(), "Under review after a report.", null, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
                rewards.Update(account);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points = 500L }));

            preview.IsPermitted.Should().BeFalse();
            preview.RefusalCode.Should().Be(FinancialErrorCodes.RewardNotEligible);
            preview.RefusalMessage.Should().Be(RewardConversionPricer.HeldMessage);
            preview.RefusalMessage.Should().NotContain("report");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    // ── The terms state the member's standing ──────────────────────────────────────────────
    //
    // The app worked eligibility out as terms.isEnabled && balance.canConvert. canConvert is false
    // below the minimum and carries no reason, so a member short of the minimum was told
    // "Conversion is not available on your account" and never saw the minimum. The terms now say
    // whether this member may convert, why not, and the most they may convert now.

    [Fact]
    public async Task Terms_say_why_a_member_below_the_minimum_cannot_convert()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            // The helper's minimum is ten points.
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 5, "grant:" + Guid.NewGuid().ToString("N"));

            var terms = await MemberTermsAsync();

            terms.IsEnabled.Should().BeTrue("the deployment converts; this member cannot yet");
            terms.Member.Should().Be(new MemberStandingShape(
                false,
                FinancialErrorCodes.RewardBelowMinimum,
                "You need at least 10 points to convert. You have 5.",
                null));
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task Terms_report_a_financial_account_that_was_never_activated()
    {
        // As in the conversion test above: verified in Identity, the outbox deliberately not drained.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, DepositCurrency);
            await GrantPointsAsync(auth.User.Id, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var member = (await MemberTermsAsync()).Member;

            member.CanConvert.Should().BeFalse();
            member.ReasonCode.Should().Be(FinancialErrorCodes.AccountNotEligible);
            member.ReasonMessage.Should().StartWith("Verify your identity");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    [Fact]
    public async Task Terms_on_a_held_account_say_only_that_it_is_held()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            using (var scope = Factory.Services.CreateScope())
            {
                var rewards = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
                var account = (await rewards.FindByMemberAsync(userId))!;

                account.BlockConversion(Guid.NewGuid(), "Velocity rule 7 tripped.", null, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
                rewards.Update(account);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            (await MemberTermsAsync()).Member.Should().Be(new MemberStandingShape(
                false, FinancialErrorCodes.RewardNotEligible, RewardConversionPricer.HeldMessage, null));
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// The app fell back to the member's balance when no maximum was stated, and labelled the
    /// balance as policy. The server now takes the minimum itself.
    /// </summary>
    [Fact]
    public async Task The_most_a_member_can_convert_now_is_the_smallest_limit()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(
                minorUnitsPerPoint: 1m, currency, maximumPoints: 600, dailyMaximumPoints: 400);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var member = (await MemberTermsAsync()).Member;

            member.CanConvert.Should().BeTrue();
            member.MaximumNow.Should().Be(400, "today's allowance is smaller than the single maximum and the balance");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// The app wrote the rate as <c>pointsPerMinorUnit × 100</c> points to one unit, right only for
    /// two-decimal currencies. The terms now carry the currency's own exponent and symbol.
    /// </summary>
    [Fact]
    public async Task Terms_state_the_currency_exponent_and_symbol()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);

            CurrencyCatalog.CurrencyFacts facts;
            using (var scope = Factory.Services.CreateScope())
            {
                facts = await scope.ServiceProvider.GetRequiredService<CurrencyCatalog>().FactsAsync(currency);
            }

            var terms = await MemberTermsAsync();

            terms.Currency.Should().Be(currency);
            terms.Exponent.Should().Be(facts.Exponent);
            terms.Symbol.Should().Be(facts.Symbol);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// A conversion cannot be undone and its credit is held (D-7). The terms never said for how long,
    /// so a member found out on the receipt, with the points already gone.
    /// </summary>
    [Fact]
    public async Task Terms_state_how_long_converted_credit_is_held()
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency, holdDays: 30);

            (await MemberTermsAsync()).HoldDays.Should().Be(30);

            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);

            (await MemberTermsAsync()).HoldDays.Should().Be(
                RewardConversionPricer.DefaultHoldDays, "a version that names no hold still holds");
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    /// <summary>
    /// Nothing in the validator reads points, so this is the whole of what stops them. A negative
    /// number used to throw in the pricer: a 500 on both routes, whether conversion was on or off.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task No_points_or_fewer_than_none_are_refused_rather_than_failing(long points)
    {
        var userId = await AuthenticateAsync();
        var currency = await MemberCurrencyAsync(userId);

        try
        {
            await PublishConversionRateAsync(minorUnitsPerPoint: 1m, currency);
            await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

            var preview = await ReadDataAsync<PreviewShape>(await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions/preview", UriKind.Relative),
                new { points }));

            preview.IsPermitted.Should().BeFalse();
            preview.RefusalCode.Should().Be(FinancialErrorCodes.RewardBelowMinimum);

            var response = await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points, idempotencyKey = Guid.NewGuid().ToString("N") });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardBelowMinimum);
            (await PointsAsync()).Should().Be(1_000);
        }
        finally
        {
            await ResetConversionRatesAsync();
        }
    }

    private async Task<MemberTermsShape> MemberTermsAsync() =>
        await ReadDataAsync<MemberTermsShape>(await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/conversion", UriKind.Relative)));

    /// <summary>The same rule at the door: an operator cannot publish a positive rate without one.</summary>
    [Fact]
    public async Task Publishing_a_positive_rate_without_a_currency_is_refused()
    {
        var (_, admin) = await SignInAsRoleAsync("Admin");

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/operations/policies", UriKind.Relative),
            new
            {
                policyKey = FinancialPolicyKeys.RewardConversion,
                kind = "RewardConversion",
                description = "A rate with no unit.",
                parameters = new Dictionary<string, string> { ["minorUnitsPerPoint"] = "1" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.RewardCurrencyRequired);
    }

    /// <summary>The points surface carries no currency, and a client that looked for one finds none.</summary>
    [Fact]
    public async Task The_points_balance_response_carries_no_currency()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me", UriKind.Relative));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = payload.GetProperty("data");

        data.TryGetProperty("currency", out _).Should().BeFalse(
            "points are a count; a currency here would let somebody add them to a balance");
    }

    /// <summary>
    /// Points spent on a reward read as spent. The entry used to say only whether it was a
    /// conversion, so a redemption reached the app as a positive entry and rendered as points
    /// earned; and the balance carried no spent total, so earned minus converted stopped equalling
    /// it after the first redemption.
    /// </summary>
    [Fact]
    public async Task A_spend_reads_as_a_spend_in_the_history_and_the_balance()
    {
        var userId = await AuthenticateAsync();

        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        using (var scope = Factory.Services.CreateScope())
        {
            var spent = await scope.ServiceProvider
                .GetRequiredService<Diaspora.Modules.Financial.Extensibility.Rewards.IRewardPointsAccount>()
                .TrySpendAsync(userId, 300, "Redemption:creator-badge-supporter", "spend:" + Guid.NewGuid().ToString("N"));

            spent.Should().Be(Diaspora.Modules.Financial.Extensibility.Rewards.RewardPointsSpendResult.Debited);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        var history = await ReadDataAsync<List<HistoryEntryShape>>(await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me/history", UriKind.Relative)));

        var spend = history.Single(entry => entry.Source == "Redemption:creator-badge-supporter");
        spend.IsSpend.Should().BeTrue();
        spend.IsConversion.Should().BeFalse();
        spend.Points.Should().Be(300, "points stay positive; the flag says which way they moved");

        history.Single(entry => entry.Source == "IntegrationTest").IsSpend.Should().BeFalse();

        var balance = await ReadDataAsync<BalanceTotalsShape>(await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me", UriKind.Relative)));

        balance.LifetimeSpent.Should().Be(300);
        balance.Balance.Should().Be(
            balance.LifetimeEarned - balance.LifetimeConverted - balance.LifetimeSpent,
            "the three totals account for the balance");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        // The financial identity is opened by an integration event this host does not dispatch on a
        // timer — see the factory — so the queue is drained by hand.
        await Factory.DrainOutboxAsync();

        await ActivateFinancialIdentityAsync(auth.User.Id);

        return auth.User.Id;
    }

    /// <summary>
    /// Puts the caller's financial identity into the state Trust puts it in.
    /// <para>
    /// <b>Arrangement, not a bypass.</b> A financial account is opened <c>Pending</c> at registration
    /// and activated by <c>VerificationApprovedIntegrationEvent</c>, which only the real Trust Engine
    /// publishes; this suite's <c>ApplyTrustDecisionAsync</c> moves an <em>identity</em> access state
    /// and raises no such event. A pending wallet refuses credit — correctly, and that rule has its
    /// own coverage — so without this every balance assertion below would be asserting a rule the
    /// suite is not about. This is the same call the Trust consumer makes.
    /// </para>
    /// </summary>
    private async Task ActivateFinancialIdentityAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider
            .GetRequiredService<IFinancialAccountRepository>();

        var identities = scope.ServiceProvider
            .GetRequiredService<FinancialIdentityService>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(userId, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Puts real money in the caller's wallet through the real deposit path — created, authorised by
    /// callback, captured by callback. Nothing here stages a balance directly, because a test that
    /// did would not be exercising the path a member uses.
    /// </summary>
    private async Task FundAsync(Guid userId, decimal amount)
    {
        var depositId = await CreateDepositAsync(amount);

        await ConfirmByWebhookAsync(depositId, (long)(amount * 100));

        (await AvailableAsync(userId)).Should().BeGreaterThan(0);
    }

    private async Task<Guid> CreateDepositAsync(decimal amount)
    {
        var response = await PostDepositAsync(amount, Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await ReadDataAsync<PaymentShape>(response)).PaymentIntentId;
    }

    private Task<HttpResponseMessage> PostDepositAsync(decimal amount, string idempotencyKey) =>
        HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative),
            new { amount, currency = "GBP", methodKind = "Card", idempotencyKey });

    /// <summary>
    /// Confirms a payment the way a provider does: authorised, then captured, both signed.
    /// </summary>
    private async Task ConfirmByWebhookAsync(Guid paymentIntentId, long minorUnits)
    {
        await PostWebhookAsync(WebhookBody("authorized", paymentIntentId, minorUnits));
        await PostWebhookAsync(WebhookBody("captured", paymentIntentId, minorUnits));
    }

    private static string WebhookBody(string kind, Guid reference, long minorUnits) =>
        "{"
        + $"\"eventKind\":\"{kind}\","
        + $"\"externalReference\":\"{reference}\","
        + $"\"eventId\":\"{Guid.NewGuid():N}\","
        + $"\"amountMinorUnits\":{minorUnits},"
        + "\"currency\":\"GBP\"}";

    private async Task PostWebhookAsync(string body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                $"/api/v1/financial/webhooks/{SimulatedPaymentGateway.ProviderName}",
                UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            SimulatedPaymentWebhookProcessor.SignatureHeader,
            SimulatedPaymentWebhookProcessor.Sign(body));

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> RegisterDestinationAsync(
        string? countryCode = "GB",
        PayoutDestinationKind kind = PayoutDestinationKind.BankAccount,
        string provider = FakeSettlementProvider.ProviderName)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/payout-destinations", UriKind.Relative),
            new
            {
                kind = kind.ToString(),
                label = "Barclays ••••4417",
                currency = "GBP",

                // Where the money is going. The router reads the country off the destination rather
                // than off the withdrawal request, so a destination without one cannot be paid to.
                countryCode,
                provider,
                providerToken = "tok_" + Guid.NewGuid().ToString("N")
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await ReadDataAsync<DestinationShape>(response)).DestinationId;
    }

    /// <summary>
    /// Registers a destination and marks it verified through the repository, because verification is
    /// a staff action and this suite has no staff token — DEBT-031, unchanged by this phase.
    /// </summary>
    private async Task<Guid> RegisterVerifiedDestinationAsync(
        string? countryCode = "GB",
        PayoutDestinationKind kind = PayoutDestinationKind.BankAccount,
        string provider = FakeSettlementProvider.ProviderName)
    {
        var destinationId = await RegisterDestinationAsync(countryCode, kind, provider);

        using var scope = Factory.Services.CreateScope();

        var destinations = scope.ServiceProvider
            .GetRequiredService<IPayoutDestinationRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var destination = await destinations.FindAsync(PayoutDestinationId.From(destinationId));

        // Verification is where the binding is made: the provider that confirms a destination is the
        // one whose token it now holds, and the router will not send it anywhere else.
        destination!.Verify(clock.UtcNow, provider, "tok_verified");
        destinations.Update(destination);

        await unitOfWork.SaveChangesAsync();

        return destinationId;
    }

    /// <summary>Reads a withdrawal straight from the store, past whatever the API would project.</summary>
    private async Task<Withdrawal> ReloadAsync(Guid withdrawalId)
    {
        using var scope = Factory.Services.CreateScope();

        var withdrawals = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();

        return (await withdrawals.FindAsync(WithdrawalId.From(withdrawalId)))!;
    }

    /// <summary>
    /// Ages a submitted withdrawal past the resolution grace window, standing in for the time a real
    /// sweep would wait before asking a provider about it.
    /// <para>
    /// Written through the context rather than the aggregate on purpose: <c>ProcessingStartedAt</c>
    /// has no setter, and it should not acquire one so that a test can move a clock.
    /// </para>
    /// </summary>
    private async Task BackdateProcessingAsync(Guid withdrawalId, TimeSpan by)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var entity = await context.Set<Withdrawal>()
            .FirstAsync(withdrawal => withdrawal.Id == WithdrawalId.From(withdrawalId));

        context.Entry(entity).Property(nameof(Withdrawal.ProcessingStartedAt))
            .CurrentValue = clock.UtcNow - by;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The reference a settlement instruction will carry for this withdrawal — the withdrawal's own
    /// identifier, which is what lets an assertion find one payout among the ones other tests left
    /// behind in the shared database.
    /// </summary>
    private static async Task<string> ReferenceOf(HttpResponseMessage created) =>
        (await ReadDataAsync<WithdrawalShape>(created)).WithdrawalId.ToString("N");

    /// <summary>
    /// Removes a payout destination directly, standing in for the hours that pass between a member
    /// asking for a payout and a sweep picking it up.
    /// </summary>
    private async Task RemoveDestinationAsync(Guid destinationId)
    {
        using var scope = Factory.Services.CreateScope();

        var destinations = scope.ServiceProvider
            .GetRequiredService<IPayoutDestinationRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var destination = await destinations.FindAsync(PayoutDestinationId.From(destinationId));

        destination!.Remove(clock.UtcNow);
        destinations.Update(destination);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>A withdrawal needs a key, so each call that names none gets a fresh one.</summary>
    private Task<HttpResponseMessage> PostWithdrawalAsync(
        decimal amount, Guid destinationId, string? idempotencyKey = null) =>
        HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative),
            new { amount, currency = "GBP", destinationId, idempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N") });

    /// <summary>
    /// Grants points through the repository, because the grant route is staff-only and this suite
    /// holds no staff token. The consumer and the route share the aggregate, so what is exercised is
    /// the same code either way.
    /// </summary>
    private async Task GrantPointsAsync(Guid userId, long points, string reference)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var account = await accounts.FindByMemberAsync(userId);

        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(guids.NewSequentialGuid()), userId, clock.UtcNow);

            accounts.Add(account);
        }

        account.Grant(guids.NewSequentialGuid(), points, "IntegrationTest", reference, clock.UtcNow);
        accounts.Update(account);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Publishes a conversion rate, which is what an operator does before points are worth anything.
    /// The seeded version prices them at zero on purpose — a rate seeded above zero would turn
    /// conversion on by deploying.
    /// <para>
    /// A rate is in one currency, so <paramref name="currency"/> is part of publishing it; passing null
    /// publishes the incomplete kind of rate that converts nothing. <paramref name="country"/> narrows
    /// the version to one country; only versions in the same scope are superseded, so a platform rate
    /// and a country rate can both be in force.
    /// </para>
    /// </summary>
    private async Task PublishConversionRateAsync(
        decimal minorUnitsPerPoint,
        string? currency,
        string? country = null,
        long? maximumPoints = null,
        long? dailyMaximumPoints = null,
        long? holdDays = null,
        long? vestingDays = null)
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider
            .GetRequiredService<IFinancialPolicyRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        var policyScope = country is null ? PolicyScopeKind.Platform : PolicyScopeKind.Country;

        var active = await policies.ListActiveByKeyAsync(
            FinancialPolicyKeys.RewardConversion);

        foreach (var superseded in active.Where(policy => policy.Scope == policyScope
            && string.Equals(policy.ScopeValue, country, StringComparison.OrdinalIgnoreCase)))
        {
            superseded.Supersede(clock.UtcNow);
            policies.Update(superseded);
        }

        var version = await policies.HighestVersionAsync(
            FinancialPolicyKeys.RewardConversion,
            policyScope,
            scopeValue: country);

        var policy = FinancialPolicy.Publish(
            FinancialPolicyId.From(guids.NewSequentialGuid()),
            FinancialPolicyKeys.RewardConversion,
            FinancialPolicyKind.RewardConversion,
            policyScope,
            scopeValue: country,
            version + 1,
            DateWindow.Open(clock.UtcNow.AddMinutes(-1)),
            "An integration-test conversion rate.",
            clock.UtcNow);

        policy.SetParameter(
            guids.NewSequentialGuid(),
            FinancialPolicyParameters.MinorUnitsPerPoint,
            minorUnitsPerPoint.ToString(System.Globalization.CultureInfo.InvariantCulture));

        policy.SetParameter(
            guids.NewSequentialGuid(),
            FinancialPolicyParameters.MinimumPoints,
            "10");

        if (currency is not null)
        {
            policy.SetParameter(
                guids.NewSequentialGuid(), FinancialPolicyParameters.Currency, currency);
        }

        if (maximumPoints is { } maximum)
        {
            policy.SetParameter(
                guids.NewSequentialGuid(),
                FinancialPolicyParameters.MaximumPoints,
                maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (dailyMaximumPoints is { } daily)
        {
            policy.SetParameter(
                guids.NewSequentialGuid(),
                FinancialPolicyParameters.DailyMaximumPoints,
                daily.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (holdDays is { } hold)
        {
            policy.SetParameter(
                guids.NewSequentialGuid(),
                FinancialPolicyParameters.HoldDays,
                hold.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (vestingDays is { } vesting)
        {
            policy.SetParameter(
                guids.NewSequentialGuid(),
                FinancialPolicyParameters.VestingDays,
                vesting.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        policies.Add(policy);

        await unitOfWork.SaveChangesAsync();

        // Conversions are paid from the rewards pot (CS-A1), and an empty pot pauses them, so the
        // suite funds the pot generously wherever it publishes a rate.
        if (currency is not null)
        {
            await SetPlatformBalanceAsync(WalletType.Rewards, currency, 100_000_000);
        }
    }

    /// <summary>
    /// Sets a platform wallet to an exact balance, moving the difference to or from Clearing. Test
    /// setup only: in a deployment, Revenue fills from fees and the pot from the staff top-up route.
    /// </summary>
    private async Task SetPlatformBalanceAsync(WalletType walletType, string currency, long minorUnits)
    {
        using var scope = Factory.Services.CreateScope();

        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var wallet = (await identities.PlatformWalletAsync(walletType, currency))!;
        var clearing = (await identities.PlatformWalletAsync(WalletType.Clearing, currency))!;
        var delta = minorUnits - wallet.AvailableMinorUnits;

        if (delta == 0)
        {
            return;
        }

        var amount = Money.FromMinorUnits(Math.Abs(delta), wallet.Currency, wallet.Exponent);
        var (from, to) = delta > 0 ? (clearing, wallet) : (wallet, clearing);

        var posted = await ledger.PostAsync(new LedgerPostingRequest
        {
            TransactionType = TransactionType.Adjustment,
            Amount = amount,
            Description = "Integration suite: set a platform balance.",
            Audit = AuditStamp.Create(
                FinancialActor.System("IntegrationSuite"),
                FinancialSource.System,
                "Integration suite: set a platform balance."),
            Legs =
            [
                new LedgerPostingLeg
                {
                    WalletId = from.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Adjustment
                },
                new LedgerPostingLeg
                {
                    WalletId = to.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = LedgerEntryType.Adjustment
                }
            ],
            Reference = $"integration-suite:{Guid.NewGuid():N}"
        });

        posted.IsSuccess.Should().BeTrue(posted.IsFailure ? posted.Error.Description : null);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>A platform wallet's available balance, in minor units.</summary>
    private async Task<long> PlatformBalanceAsync(WalletType walletType, string currency)
    {
        using var scope = Factory.Services.CreateScope();

        var wallet = await scope.ServiceProvider
            .GetRequiredService<IWalletRepository>()
            .FindPlatformWalletAsync(walletType, currency);

        return wallet!.AvailableMinorUnits;
    }

    /// <summary>Back to the seed: no country rates, and a platform rate of zero.</summary>
    private async Task ResetConversionRatesAsync()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var policies = scope.ServiceProvider.GetRequiredService<IFinancialPolicyRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            foreach (var country in (await policies.ListActiveByKeyAsync(FinancialPolicyKeys.RewardConversion))
                .Where(policy => policy.Scope == PolicyScopeKind.Country))
            {
                country.Supersede(clock.UtcNow);
                policies.Update(country);
            }

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await PublishConversionRateAsync(minorUnitsPerPoint: 0m, currency: null);
    }

    private async Task SetAccountCountryAsync(Guid userId, string country)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal)
            ?? throw new InvalidOperationException($"Member {userId} has no financial account.");

        account.SetCountry(country, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        accounts.Update(account);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<long> AvailableAsync(Guid userId, string currency = DepositCurrency) =>
        (await PrimaryWalletAsync(userId, currency))?.AvailableMinorUnits ?? 0;

    private async Task<long> PendingAsync(Guid userId, string currency = DepositCurrency) =>
        (await PrimaryWalletAsync(userId, currency))?.PendingMinorUnits ?? 0;

    private async Task<long> ReservedAsync(Guid userId, string currency = DepositCurrency) =>
        (await PrimaryWalletAsync(userId, currency))?.ReservedMinorUnits ?? 0;

    /// <summary>
    /// The member's primary wallet in one currency.
    /// </summary>
    /// <remarks>
    /// <b>A member holds more than one.</b> Registration opens a wallet in the currency of their
    /// country of residence, and a deposit in another currency opens a second — both
    /// <c>Primary</c>. So the currency is part of the question, and a lookup that dropped it
    /// returned whichever happened to come back first, which is how a funded wallet reads as empty.
    /// </remarks>
    private async Task<Wallet?> PrimaryWalletAsync(Guid userId, string currency = DepositCurrency)
    {
        using var scope = Factory.Services.CreateScope();

        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var held = await wallets.ListByOwnerAsync(userId);

        return held.FirstOrDefault(wallet =>
            wallet.WalletType == WalletType.Primary
            && string.Equals(wallet.Currency, currency, StringComparison.Ordinal));
    }

    /// <summary>
    /// The currency this member's financial account was opened in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the account rather than hard-coded, because it follows the member's
    /// <c>CountryOfResidence</c>: <c>IdentityTestBase</c> registers members resident in Canada, so
    /// the platform opens them a CAD account. Read from the <em>account</em> rather than a wallet
    /// because no wallet exists until something credits one — which, for a conversion, is the very
    /// call under test.
    /// </para>
    /// <para>
    /// It is also the comparison the handler itself makes: a conversion credits a wallet in the
    /// currency the caller names, and a member holds one only in the currency their account was
    /// opened in. Naming any other currency is answered
    /// <c>404 — "You hold no GBP wallet for the credit to land in."</c>, which is correct.
    /// </para>
    /// <para>
    /// <b>These tests used to post "GBP" and pass, and that was the bug passing.</b> Every wallet
    /// was opened in GBP regardless of country — a Mauritian member's wallet screen read
    /// "£0.00 / GBP" — which is the defect <c>CountryCurrency</c> was written to fix. The tests
    /// had encoded it, so fixing the product turned two of them red. Asking the wallet what it
    /// holds is the assertion that survives the next country being added.
    /// </para>
    /// </remarks>
    private async Task<string> MemberCurrencyAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider
            .GetRequiredService<IFinancialAccountRepository>();

        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal);

        return account?.PrimaryCurrency
            ?? throw new InvalidOperationException(
                $"Member {userId} has no financial account, so there is no currency to convert "
                + "into. AuthenticateAsync drains the outbox and activates the identity, so this "
                + "means the account was never opened.");
    }

    /// <summary>A staff grant over HTTP, as whoever the client is signed in as.</summary>
    private Task<HttpResponseMessage> PostGrantAsync(
        Guid userId, long points, string reference, string? reason = "Integration suite: a missed earning.") =>
        HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/operations/rewards/grants", UriKind.Relative),
            new { userId, points, source = "StaffAdjustment", grantReference = reference, reason });

    /// <summary>Signs the client in as one of several staff members already signed in once.</summary>
    private void ActAs(Diaspora.Contracts.Identity.V1.Responses.AuthenticationResponse auth)
    {
        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    /// <summary>Asks for a grant that needs a second Admin, as whoever the client is signed in as.</summary>
    private Task<HttpResponseMessage> PostGrantRequestAsync(
        Guid userId, long points, string reference, string? reason = "Integration suite: a large missed earning.") =>
        HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/operations/rewards/grant-requests", UriKind.Relative),
            new { userId, points, source = "StaffAdjustment", grantReference = reference, reason });

    private Task<HttpResponseMessage> DecideGrantRequestAsync(Guid requestId, bool approve, string? note) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"/api/v1/financial/operations/rewards/grant-requests/{requestId}/decide", UriKind.Relative),
            new { approve, note });

    private static async Task<Guid> RequestIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"the request should have been taken, but answered {body}");

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").GetProperty("requestId").GetGuid();
    }

    private static async Task<string?> StatusOfAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("status").GetString();
    }

    /// <summary>Holds or lifts a member's conversion hold through the aggregate, as a system decision.</summary>
    private async Task SetConversionHoldAsync(Guid userId, bool held)
    {
        using var scope = Factory.Services.CreateScope();

        var rewards = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var account = (await rewards.FindByMemberAsync(userId))!;

        if (held)
        {
            account.BlockConversion(Guid.NewGuid(), "Integration suite: under review.", null, now);
        }
        else
        {
            account.AllowConversion(Guid.NewGuid(), "Integration suite: review cleared.", null, now);
        }

        rewards.Update(account);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <summary>A staff top-up of a rewards pot over HTTP, as whoever the client is signed in as.</summary>
    private Task<HttpResponseMessage> PostTopUpAsync(string currency, decimal amount, string reason) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"/api/v1/financial/operations/rewards/pots/{currency}/top-ups", UriKind.Relative),
            new { amount, reason });

    /// <summary>A staff hold decision over HTTP, as whoever the client is signed in as.</summary>
    private Task<HttpResponseMessage> PutHoldAsync(
        Guid userId, bool isHeld, string? reason = "Integration suite: under review.") =>
        HttpClient.PutAsJsonAsync(
            new Uri($"/api/v1/financial/operations/rewards/members/{userId}/conversion-hold", UriKind.Relative),
            new { isHeld, reason });

    /// <summary>The staff review queue over HTTP, as whoever the client is signed in as.</summary>
    private Task<HttpResponseMessage> GetHoldsAsync(
        DateTimeOffset? before = null, IReadOnlyList<Guid>? excludeIds = null, int? take = null)
    {
        var query = new List<string>();

        if (before is { } boundary)
        {
            query.Add("before=" + Uri.EscapeDataString(boundary.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
        }

        query.AddRange((excludeIds ?? []).Select(id => $"excludeIds={id}"));

        if (take is { } size)
        {
            query.Add($"take={size}");
        }

        var path = "/api/v1/financial/operations/rewards/holds"
            + (query.Count == 0 ? string.Empty : "?" + string.Join('&', query));

        return HttpClient.GetAsync(new Uri(path, UriKind.Relative));
    }

    /// <summary>Signs the client back in with a token it held earlier.</summary>
    private void UseToken(string token)
    {
        Api.Authenticate(token);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>A member's balance, read without signing in as them.</summary>
    private async Task<long> BalanceOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var standing = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindStandingAsync(userId);

        return standing?.Balance ?? 0;
    }

    /// <summary>A grantor's points arriving through the consumer, as Creator's and Trust's do.</summary>
    private async Task DeliverGrantAsync(
        Guid userId, long points, DateTimeOffset grantedAt, string? reference = null)
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<
            Diaspora.Modules.Financial.Application.Consumers.Rewards.RewardPointsGrantedConsumer>(
            scope.ServiceProvider);

        await consumer.Handle(
            new IntegrationEventNotification<RewardPointsGrantedIntegrationEvent>(
                new RewardPointsGrantedIntegrationEvent
                {
                    MemberUserId = userId,
                    Points = points,
                    Source = "IntegrationTest",
                    GrantReference = reference ?? "grant:" + Guid.NewGuid().ToString("N"),
                    GrantedAt = grantedAt
                }),
            CancellationToken.None);
    }

    /// <summary>A grantor withdrawing a grant through the consumer, as Trust will (Part C).</summary>
    private async Task DeliverReversalAsync(Guid userId, string reference, string reason)
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<
            Diaspora.Modules.Financial.Application.Consumers.Rewards.RewardPointsReversedConsumer>(
            scope.ServiceProvider);

        await consumer.Handle(
            new IntegrationEventNotification<RewardPointsReversedIntegrationEvent>(
                new RewardPointsReversedIntegrationEvent
                {
                    MemberUserId = userId,
                    GrantReference = reference,
                    Reason = reason
                }),
            CancellationToken.None);
    }

    private async Task<RewardAccount> RewardAccountOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return (await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(userId))!;
    }

    private async Task<long> PendingPointsOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(userId);

        return account?.PendingBalance ?? 0;
    }

    private async Task<RewardBalanceShape> RewardBalanceAsync() =>
        await ReadDataAsync<RewardBalanceShape>(await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me", UriKind.Relative)));

    private async Task<long> PointsAsync()
    {
        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/rewards/me", UriKind.Relative));

        return (await ReadDataAsync<RewardBalanceShape>(response)).Balance;
    }

    private async Task<int> RunSweepAsync<TSweep>() where TSweep : FinancialSweep
    {
        var sweep = Factory.Services
            .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<TSweep>()
            .Single();

        return await sweep.RunOnceAsync();
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record MoneyShape(decimal Amount, string Currency, long MinorUnits);

    private sealed record PotShape(string Currency, MoneyShape Available);

    private sealed record HoldShape(Guid UserId, bool IsHeld, string? Reason, List<HoldDecisionShape> Decisions);

    private sealed record HoldDecisionShape(bool IsHeld, string Reason, Guid? DecidedByUserId, DateTimeOffset DecidedAt);

    private sealed record HeldAccountShape(
        Guid UserId, long Balance, long PendingBalance, HoldDecisionShape LatestDecision);

    private sealed record PaymentShape(Guid PaymentIntentId, string Status, string Purpose);

    private sealed record WithdrawalShape(
        Guid WithdrawalId,
        string Status,
        MoneyShape Gross,
        MoneyShape Fee,
        MoneyShape Net,
        bool IsCancellable);

    private sealed record PayoutCapabilitiesShape(
        bool PayoutsEnabled,
        List<string> PayoutCountries);

    private sealed record DestinationShape(Guid DestinationId, string Label, bool IsVerified);

    private sealed record LimitsShape(
        bool IsEnabled, MoneyShape? Minimum, MoneyShape? Maximum, MoneyShape? Available);

    private sealed record ConversionShape(
        Guid ConversionId,
        long Points,
        MoneyShape Credited,
        Guid? TransactionId,
        string Status = "Available",
        DateTimeOffset? AvailableAt = null);

    private sealed record RewardBalanceShape(
        long Balance,
        long LifetimeEarned,
        bool CanConvert,
        long PendingPoints = 0,
        DateTimeOffset? NextVestsAt = null,
        long LifetimeReversed = 0);

    private sealed record TermsShape(
        bool IsEnabled, string? DisabledReason, string Currency, decimal MinorUnitsPerPoint);

    private sealed record MemberTermsShape(
        bool IsEnabled, string Currency, int Exponent, string? Symbol, int HoldDays, MemberStandingShape Member);

    private sealed record MemberStandingShape(
        bool CanConvert, string? ReasonCode, string? ReasonMessage, long? MaximumNow);

    private sealed record PreviewShape(
        long Points,
        long PointsConsumed,
        decimal MinorUnitsPerPoint,
        bool IsPermitted,
        string? RefusalCode,
        string? RefusalMessage = null);

    private sealed record RoundedConversionShape(long Points, long? PointsRequested, MoneyShape Credited);

    private sealed record HistoryEntryShape(long Points, bool IsConversion, bool IsSpend, string Source);

    private sealed record BalanceTotalsShape(
        long Balance, long LifetimeEarned, long LifetimeConverted, long LifetimeSpent);

}

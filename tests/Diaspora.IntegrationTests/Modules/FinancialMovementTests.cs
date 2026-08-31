using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
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
                countryCode = "GB"
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

        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/financial/wallets/me/withdrawals/limits", UriKind.Relative));

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
            new { points = 500L, currency = "GBP" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(response))
            .Should().Be(FinancialErrorCodes.RewardConversionNotSupported);
    }

    /// <summary>
    /// <b>The one crossing, with a real ledger posting behind it.</b> Points come out of a store that
    /// is not money; the credit comes out of the platform's revenue wallet, which is where the cost
    /// of a rewards programme actually lands.
    /// </summary>
    [Fact]
    public async Task Converting_points_credits_the_wallet_and_posts_to_the_ledger()
    {
        var userId = await AuthenticateAsync();

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m);
        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        var before = await AvailableAsync(userId);

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 600L, currency = "GBP", idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var conversion = await ReadDataAsync<ConversionShape>(response);

        conversion.Points.Should().Be(600);
        conversion.Credited.MinorUnits.Should().Be(600);
        conversion.TransactionId.Should().NotBeNull("a conversion is a ledger posting, not a counter");

        (await AvailableAsync(userId)).Should().Be(before + 600);
        (await PointsAsync()).Should().Be(400);
    }

    /// <summary>A double-tapped convert button converts once.</summary>
    [Fact]
    public async Task Two_conversions_carrying_one_idempotency_key_convert_once()
    {
        var userId = await AuthenticateAsync();

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m);
        await GrantPointsAsync(userId, 1_000, "grant:" + Guid.NewGuid().ToString("N"));

        var key = Guid.NewGuid().ToString("N");

        await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 300L, currency = "GBP", idempotencyKey = key });

        await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 300L, currency = "GBP", idempotencyKey = key });

        (await PointsAsync()).Should().Be(700, "points leave once per key");
    }

    [Fact]
    public async Task Converting_more_points_than_the_member_holds_is_refused()
    {
        var userId = await AuthenticateAsync();

        await PublishConversionRateAsync(minorUnitsPerPoint: 1m);
        await GrantPointsAsync(userId, 100, "grant:" + Guid.NewGuid().ToString("N"));

        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
            new { points = 5_000L, currency = "GBP" });

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

    private Task<HttpResponseMessage> PostWithdrawalAsync(
        decimal amount, Guid destinationId, string? idempotencyKey = null) =>
        HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative),
            new { amount, currency = "GBP", destinationId, idempotencyKey });

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
    /// </summary>
    private async Task PublishConversionRateAsync(decimal minorUnitsPerPoint)
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider
            .GetRequiredService<IFinancialPolicyRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        var active = await policies.ListActiveByKeyAsync(
            FinancialPolicyKeys.RewardConversion);

        foreach (var superseded in active)
        {
            superseded.Supersede(clock.UtcNow);
            policies.Update(superseded);
        }

        var version = await policies.HighestVersionAsync(
            FinancialPolicyKeys.RewardConversion,
            PolicyScopeKind.Platform,
            scopeValue: null);

        var policy = FinancialPolicy.Publish(
            FinancialPolicyId.From(guids.NewSequentialGuid()),
            FinancialPolicyKeys.RewardConversion,
            FinancialPolicyKind.RewardConversion,
            PolicyScopeKind.Platform,
            scopeValue: null,
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

        policies.Add(policy);

        await unitOfWork.SaveChangesAsync();
    }

    private async Task<long> AvailableAsync(Guid userId) =>
        (await PrimaryWalletAsync(userId))?.AvailableMinorUnits ?? 0;

    private async Task<long> ReservedAsync(Guid userId) =>
        (await PrimaryWalletAsync(userId))?.ReservedMinorUnits ?? 0;

    private async Task<Wallet?> PrimaryWalletAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var wallets = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var held = await wallets.ListByOwnerAsync(userId);

        return held.FirstOrDefault(wallet =>
            wallet.WalletType == WalletType.Primary
            && string.Equals(wallet.Currency, "GBP", StringComparison.Ordinal));
    }

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
        Guid ConversionId, long Points, MoneyShape Credited, Guid? TransactionId);

    private sealed record RewardBalanceShape(long Balance, long LifetimeEarned, bool CanConvert);

}

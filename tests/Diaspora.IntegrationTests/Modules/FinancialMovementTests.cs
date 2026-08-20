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
using Diaspora.Modules.Financial.Infrastructure.Hosting;
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
    /// A provider that throws is treated as a refusal rather than taking the pass down. A member
    /// whose funds are stuck is a worse outcome than a reconciliation mismatch with a person
    /// attached.
    /// </summary>
    [Fact]
    public async Task A_settlement_provider_that_throws_releases_the_hold()
    {
        var userId = await AuthenticateAsync();

        await FundAsync(userId, 100m);

        var destinationId = await RegisterVerifiedDestinationAsync();

        await PostWithdrawalAsync(40m, destinationId);

        Factory.Settlement.Throws = true;

        await RunSweepAsync<WithdrawalProcessingSweep>();

        Factory.Settlement.Throws = false;

        (await ReservedAsync(userId)).Should().Be(0);
        (await AvailableAsync(userId)).Should().Be(10_000);
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

    private async Task<Guid> RegisterDestinationAsync()
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/payout-destinations", UriKind.Relative),
            new
            {
                kind = nameof(PayoutDestinationKind.BankAccount),
                label = "Barclays ••••4417",
                currency = "GBP",
                provider = "test-settlement",
                providerToken = "tok_" + Guid.NewGuid().ToString("N")
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await ReadDataAsync<DestinationShape>(response)).DestinationId;
    }

    /// <summary>
    /// Registers a destination and marks it verified through the repository, because verification is
    /// a staff action and this suite has no staff token — DEBT-031, unchanged by this phase.
    /// </summary>
    private async Task<Guid> RegisterVerifiedDestinationAsync()
    {
        var destinationId = await RegisterDestinationAsync();

        using var scope = Factory.Services.CreateScope();

        var destinations = scope.ServiceProvider
            .GetRequiredService<IPayoutDestinationRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var destination = await destinations.FindAsync(PayoutDestinationId.From(destinationId));

        destination!.Verify(clock.UtcNow, "test-settlement", "tok_verified");
        destinations.Update(destination);

        await unitOfWork.SaveChangesAsync();

        return destinationId;
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

    private sealed record DestinationShape(Guid DestinationId, string Label, bool IsVerified);

    private sealed record LimitsShape(
        bool IsEnabled, MoneyShape? Minimum, MoneyShape? Maximum, MoneyShape? Available);

    private sealed record ConversionShape(
        Guid ConversionId, long Points, MoneyShape Credited, Guid? TransactionId);

    private sealed record RewardBalanceShape(long Balance, long LifetimeEarned, bool CanConvert);

}

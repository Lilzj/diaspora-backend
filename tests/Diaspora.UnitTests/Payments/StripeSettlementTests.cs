using System.Globalization;
using System.Net;
using System.Text;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// What the Stripe adapter puts on the wire, and what it does with what comes back.
/// <para>
/// <b>No network and no key.</b> The handler is a stub, so these run in CI and on a laptop with no
/// Stripe account — which is the point: the request shape and the response classification are the
/// parts that ship, and neither needs a live account to be wrong. <c>LiveStripeSandboxTests</c>
/// covers the account itself and is skipped unless credentials are present.
/// </para>
/// </summary>
public class StripeSettlementTests
{
    [Fact]
    public async Task A_transfer_carries_the_amount_the_currency_and_the_connected_account()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        var outcome = await Provider(handler).SubmitAsync(Instruction());

        outcome.IsAccepted.Should().BeTrue();
        outcome.ProviderReference.Should().Be("tr_123", "the transfer id is what reconciliation matches on");

        handler.Path.Should().Be("/v1/transfers", "a Connect transfer, not a payout of our own balance");
        handler.Form["amount"].Should().Be("1000");
        handler.Form["currency"].Should().Be("gbp", "Stripe rejects an upper-case currency code");
        handler.Form["destination"].Should().Be("acct_123");
    }

    /// <summary>
    /// <b>The idempotency key is the withdrawal's own identifier.</b> A key derived from the clock or
    /// a fresh Guid would make every retry a second transfer, which is the exact failure the header
    /// exists to prevent — and the sweep's "record Processing, then submit" shape is only worth
    /// anything if a repeat is safe.
    /// </summary>
    [Fact]
    public async Task Every_transfer_carries_an_idempotency_key_derived_from_the_withdrawal()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");
        var instruction = Instruction();

        await Provider(handler).SubmitAsync(instruction);

        handler.IdempotencyKey.Should().Be("diaspora-payout-abc123");

        // The same instruction submitted again must reuse the key, not mint a new one.
        var second = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        await Provider(second).SubmitAsync(instruction);

        second.IdempotencyKey.Should().Be(handler.IdempotencyKey);
    }

    [Fact]
    public async Task The_secret_key_is_sent_as_a_bearer_token_and_the_api_version_is_pinned()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        await Provider(handler).SubmitAsync(Instruction());

        handler.Authorization.Should().Be("Bearer sk_test_stub");
        handler.StripeVersion.Should().NotBeNullOrWhiteSpace(
            "an unpinned version changes behaviour on a date nobody chose");
    }

    /// <summary>
    /// A destination token belongs to the provider that issued it. Passing one from elsewhere to
    /// Stripe would either be rejected or, worse, matched against something of Stripe's own.
    /// </summary>
    [Fact]
    public async Task An_instruction_for_another_providers_destination_is_refused_without_a_call()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        var outcome = await Provider(handler).SubmitAsync(
            Instruction() with { DestinationProvider = "flutterwave" });

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue("nothing was sent, so the money is still ours");
        handler.Called.Should().BeFalse("nothing should reach Stripe");
    }

    [Fact]
    public async Task An_instruction_with_no_connected_account_is_refused_without_a_call()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        var outcome = await Provider(handler).SubmitAsync(
            Instruction() with { DestinationReference = null });

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue();
        handler.Called.Should().BeFalse();
    }

    /// <summary>
    /// <b>A 4xx is a refusal and means no money moved</b>, so the caller may safely release the hold.
    /// </summary>
    [Fact]
    public async Task A_rejected_transfer_is_a_refusal_rather_than_an_unknown_outcome()
    {
        var handler = new StubHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"type":"invalid_request_error","code":"balance_insufficient","message":"Insufficient funds."}}""");

        var outcome = await Provider(handler).SubmitAsync(Instruction());

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue(
            "Stripe declining to create the transfer means no money moved");
        outcome.Code.Should().Be("stripe.http_400");
    }

    /// <summary>
    /// <b>A 5xx, a timeout or a 409 is not a refusal.</b> The transfer may exist. Returning false
    /// would tell the caller that no money moved, which is a statement this adapter cannot make.
    /// 409 is included because Stripe returns it when an idempotency key is replayed while the first
    /// request is still in flight — which means one is being created right now.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task An_unknown_outcome_is_raised_rather_than_reported_as_a_refusal(
        HttpStatusCode status)
    {
        var handler = new StubHandler(status, """{"error":{"message":"try again"}}""");

        var outcome = await Provider(handler).SubmitAsync(Instruction());

        outcome.Disposition.Should().Be(SettlementDisposition.Unknown);

        // The assertion this whole change exists for. Releasing a hold on an unknown outcome is how
        // a member is paid by the provider and given their balance back by the platform.
        outcome.MayReleaseHold.Should().BeFalse();
        outcome.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task A_transport_failure_is_an_unknown_outcome()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "")
        {
            Throw = new HttpRequestException("connection reset")
        };

        var outcome = await Provider(handler).SubmitAsync(Instruction());

        outcome.Disposition.Should().Be(SettlementDisposition.Unknown);
        outcome.MayReleaseHold.Should().BeFalse(
            "a request that may have reached Stripe may have moved money");
    }

    /// <summary>
    /// Zero-decimal currencies are already correct because <c>Money</c> stores minor units and
    /// carries its own exponent — a yen amount is a count of yen, which is what Stripe wants.
    /// </summary>
    [Fact]
    public async Task A_zero_decimal_currency_is_sent_as_its_own_unit()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"tr_123"}""");

        await Provider(handler).SubmitAsync(Instruction() with
        {
            Amount = Money.FromMinorUnits(2_500, "JPY", exponent: 0)
        });

        handler.Form["amount"].Should().Be("2500");
        handler.Form["currency"].Should().Be("jpy");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static StripeSettlementProvider Provider(StubHandler handler) =>
        new(
            new StaticOptions(new StripeOptions
            {
                SecretKey = "sk_test_stub",
                ApiBase = "https://stripe.test"
            }),
            new HttpClient(handler) { BaseAddress = new Uri("https://stripe.test") },
            NullLogger<StripeSettlementProvider>.Instance);

    private static SettlementInstruction Instruction() =>
        new()
        {
            CounterpartyId = Guid.NewGuid(),
            Amount = Money.FromMinorUnits(1_000, "GBP"),
            PeriodStart = DateTimeOffset.UnixEpoch,
            PeriodEnd = DateTimeOffset.UnixEpoch,
            Reference = "abc123",
            DestinationReference = "acct_123",
            DestinationProvider = "stripe",
            DestinationKind = PayoutDestinationKind.BankAccount,
            DestinationCountry = "GB"
        };

    private sealed class StaticOptions(StripeOptions value) : IOptionsMonitor<StripeOptions>
    {
        public StripeOptions CurrentValue => value;

        public StripeOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<StripeOptions, string?> listener) => null;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public bool Called { get; private set; }

        public string? Path { get; private set; }

        public string? Authorization { get; private set; }

        public string? StripeVersion { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public Dictionary<string, string> Form { get; } = new(StringComparer.Ordinal);

        public Exception? Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();

            StripeVersion = request.Headers.TryGetValues("Stripe-Version", out var version)
                ? string.Join(",", version)
                : null;

            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var key)
                ? string.Join(",", key)
                : null;

            if (request.Content is not null)
            {
                var encoded = await request.Content.ReadAsStringAsync(cancellationToken);

                foreach (var pair in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);

                    Form[Uri.UnescapeDataString(parts[0])] =
                        parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
                }
            }

            if (Throw is not null)
            {
                throw Throw;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// <b>The default disposition is Unknown, not Accepted or Refused.</b> An adapter that forgets
    /// to say lands on the answer that holds the money rather than the one that gives it away.
    /// </summary>
    [Fact]
    public void An_outcome_that_says_nothing_holds_the_money()
    {
        var outcome = SettlementOutcome.Unresolved("x");

        outcome.IsAccepted.Should().BeFalse();
        outcome.MayReleaseHold.Should().BeFalse();
        default(SettlementDisposition).Should().Be(SettlementDisposition.Unknown);
    }
}

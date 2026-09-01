using System.Net;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The Stripe charge adapter.
/// <para>
/// <b>Hosted Checkout rather than a bare client secret, and that is forced by the client.</b> A
/// PaymentIntent client secret has to be confirmed by Stripe's own SDK; the app has no payment SDK
/// at all. What it does have is a challenge renderer that opens an HTTPS URL, so a hosted Checkout
/// page is the only thing a member can actually complete today.
/// </para>
/// <para>
/// <b>The properties here are the ones that only fail in production.</b> The request shape, the
/// per-operation idempotency key, the session-to-intent resolution, and above all the difference
/// between "declined" and "we do not know" — a charge read as declined when it in fact succeeded
/// leaves a member charged and unserved.
/// </para>
/// </summary>
public class StripePaymentGatewayTests
{
    // ── Opening a Checkout ───────────────────────────────────────────────────

    [Fact]
    public async Task Authorising_opens_a_manual_capture_checkout_in_minor_units()
    {
        var handler = StripeStub.Checkout();

        await Gateway(handler).AuthorizeAsync(Request());

        handler.LastPath.Should().Be("/v1/checkout/sessions");

        // Manual, or completing Checkout takes the money and the platform loses the ability to
        // cancel before capture.
        handler.Form["payment_intent_data[capture_method]"].Should().Be("manual");
        handler.Form["line_items[0][price_data][unit_amount]"].Should().Be("1000");

        // Stripe rejects an upper-case code as an unknown currency rather than normalising it.
        handler.Form["line_items[0][price_data][currency]"].Should().Be("gbp");
    }

    [Fact]
    public async Task The_member_is_sent_to_the_hosted_page_and_the_session_is_what_is_stored()
    {
        var handler = StripeStub.Checkout();

        var result = await Gateway(handler).AuthorizeAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.RequiresAction);
        result.NextActionPayload.Should().Be("https://checkout.stripe.com/c/pay/cs_test_1");
        result.ExternalReference.Should().Be("cs_test_1");
    }

    [Fact]
    public async Task The_platform_payment_id_is_carried_as_the_client_reference()
    {
        // This is the whole correlation. A session has no Stripe PaymentIntent id until the member
        // pays, so the callback cannot be matched on one; ProcessPaymentWebhookCommandHandler falls
        // back to finding an intent by the platform identifier, which is this field.
        var handler = StripeStub.Checkout();
        var request = Request();

        await Gateway(handler).AuthorizeAsync(request);

        handler.Form["client_reference_id"].Should().Be(request.PaymentIntentId.ToString("N"));
        handler.Form["metadata[diaspora_payment_intent]"].Should().Be(request.PaymentIntentId.ToString("N"));
    }

    [Fact]
    public async Task The_return_urls_are_sent_because_stripe_refuses_a_checkout_without_them()
    {
        var handler = StripeStub.Checkout();

        await Gateway(handler).AuthorizeAsync(Request());

        handler.Form["success_url"].Should().Be("https://diaspora.test/wallet/paid");
        handler.Form["cancel_url"].Should().Be("https://diaspora.test/wallet/cancelled");
    }

    [Fact]
    public async Task A_key_with_no_return_urls_refuses_the_charge_and_names_the_setting()
    {
        // A deployment may hold a Stripe key for payouts alone. That must refuse the charge with
        // something an operator can act on, not fail somewhere further in with a 400.
        var handler = StripeStub.Checkout();

        var gateway = new StripePaymentGateway(
            new StaticStripeOptions(new StripeOptions
            {
                SecretKey = "sk_test_stub",
                ApiBase = "https://stripe.test",
            }),
            new HttpClient(handler) { BaseAddress = new Uri("https://stripe.test") },
            NullLogger<StripePaymentGateway>.Instance);

        var result = await gateway.AuthorizeAsync(Request());

        handler.Called.Should().BeFalse("there is nowhere to return the member to");
        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
        result.FailureMessage.Should().Contain("CheckoutSuccessUrl");
    }

    [Fact]
    public async Task A_session_with_no_url_is_unavailable_rather_than_a_silent_challenge()
    {
        var handler = new StripeStub((_, _) => (HttpStatusCode.OK, """{"id":"cs_test_1"}"""));

        var result = await Gateway(handler).AuthorizeAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
    }

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Authorise_and_capture_never_share_an_idempotency_key()
    {
        // The defect this prevents: Stripe replays the first response it saw for a key, so a capture
        // sent under the authorisation's key returns the authorisation's body — which reads as a
        // success while capturing nothing at all.
        var request = Request();

        var authorize = StripeStub.Checkout();
        await Gateway(authorize).AuthorizeAsync(request);

        var capture = StripeStub.CompletedSessionThen(Intent("succeeded"));
        await Gateway(capture).CaptureAsync(request with { ExternalReference = "cs_test_1" });

        authorize.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
        capture.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
        capture.IdempotencyKey.Should().NotBe(authorize.IdempotencyKey);
    }

    [Fact]
    public async Task The_same_payment_authorised_twice_reuses_its_key_so_stripe_replays()
    {
        var request = Request();

        var first = StripeStub.Checkout();
        await Gateway(first).AuthorizeAsync(request);

        var second = StripeStub.Checkout();
        await Gateway(second).AuthorizeAsync(request);

        second.IdempotencyKey.Should().Be(first.IdempotencyKey);
    }

    // ── Session resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task Capturing_resolves_the_session_to_the_intent_stripe_created()
    {
        // What the platform stored is a cs_..., and no PaymentIntent existed when it was stored.
        var handler = StripeStub.CompletedSessionThen(Intent("succeeded"));

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        handler.Paths.Should().ContainInOrder(
            "/v1/checkout/sessions/cs_test_1",
            "/v1/payment_intents/pi_123/capture");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_reference_that_is_already_an_intent_is_not_resolved_again()
    {
        // Keeps this correct for an intent created any way other than Checkout, and saves a call.
        var handler = StripeStub.Always(Intent("succeeded"));

        await Gateway(handler).CaptureAsync(Request() with { ExternalReference = "pi_123" });

        handler.Paths.Should().ContainSingle()
            .Which.Should().Be("/v1/payment_intents/pi_123/capture");
    }

    [Fact]
    public async Task An_unfinished_checkout_is_refused_with_a_reason_rather_than_captured()
    {
        // The member never paid, so there is nothing to capture. Saying that is more useful than a
        // 400 from Stripe about a null intent.
        var handler = new StripeStub((_, _) =>
            (HttpStatusCode.OK, """{"id":"cs_test_1","payment_intent":null}"""));

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("stripe.checkout_not_completed");
    }

    [Fact]
    public async Task Capturing_a_payment_with_no_recorded_reference_is_refused_before_any_call()
    {
        var handler = StripeStub.Always(Intent("succeeded"));

        var result = await Gateway(handler).CaptureAsync(Request() with { ExternalReference = null });

        handler.Called.Should().BeFalse("there is nothing to address the capture to");
        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("stripe.no_intent_reference");
    }

    [Fact]
    public async Task Capturing_sends_the_amount_so_a_partial_capture_is_possible()
    {
        var handler = StripeStub.CompletedSessionThen(Intent("succeeded"));

        await Gateway(handler).CaptureAsync(Request() with { ExternalReference = "cs_test_1" });

        handler.Form["amount_to_capture"].Should().Be("1000");
    }

    [Fact]
    public async Task Cancelling_resolves_and_addresses_the_intent()
    {
        var handler = StripeStub.CompletedSessionThen(Intent("canceled"));

        var result = await Gateway(handler).CancelAsync(
            Request() with { ExternalReference = "cs_test_1" });

        handler.LastPath.Should().Be("/v1/payment_intents/pi_123/cancel");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_refund_is_created_against_the_resolved_intent()
    {
        var handler = StripeStub.CompletedSessionThen("""{"id":"re_1","status":"succeeded"}""");

        var result = await Gateway(handler).RefundAsync(
            Request() with { ExternalReference = "cs_test_1" });

        handler.LastPath.Should().Be("/v1/refunds");
        handler.Form["payment_intent"].Should().Be("pi_123");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_pending_refund_is_not_reported_to_the_member_as_a_refusal()
    {
        // It is accepted and on its way. Wording that as a failure teaches members the refund did
        // not happen when it is about to.
        var handler = StripeStub.CompletedSessionThen("""{"id":"re_1","status":"pending"}""");

        var result = await Gateway(handler).RefundAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Succeeded.Should().BeTrue();
        result.ExternalStatus.Should().Be("pending");
    }

    [Fact]
    public async Task A_failed_refund_is_a_decline_and_says_why()
    {
        var handler = StripeStub.CompletedSessionThen(
            """{"id":"re_1","status":"failed","failure_reason":"expired_or_canceled_card"}""");

        var result = await Gateway(handler).RefundAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureMessage.Should().Be("expired_or_canceled_card");
    }

    // ── Reading an intent ────────────────────────────────────────────────────

    [Theory]
    [InlineData("requires_capture")]
    [InlineData("succeeded")]
    [InlineData("canceled")]
    public async Task A_held_taken_or_cancelled_intent_succeeds(string status)
    {
        var handler = StripeStub.CompletedSessionThen(Intent(status));

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Succeeded.Should().BeTrue();
        result.ExternalStatus.Should().Be(status);
    }

    [Fact]
    public async Task An_unrecognised_status_is_unavailable_rather_than_assumed_good()
    {
        var handler = StripeStub.CompletedSessionThen(Intent("something_new"));

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
    }

    [Fact]
    public async Task A_body_with_no_id_is_unavailable_because_the_payment_could_never_be_named()
    {
        var handler = StripeStub.CompletedSessionThen("""{"status":"succeeded"}""");

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
    }

    // ── Declined vs unknown ──────────────────────────────────────────────────

    [Fact]
    public async Task A_card_decline_carries_the_issuers_reason_rather_than_stripes()
    {
        // "insufficient_funds" tells a member what to do; "card_declined" does not.
        var handler = new StripeStub((_, _) => (
            HttpStatusCode.PaymentRequired,
            """{"error":{"code":"card_declined","decline_code":"insufficient_funds","message":"Your card has insufficient funds."}}"""));

        var result = await Gateway(handler).AuthorizeAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("stripe.insufficient_funds");
        result.FailureMessage.Should().Be("Your card has insufficient funds.");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task An_answer_that_does_not_settle_the_charge_is_retryable_not_declined(
        HttpStatusCode status)
    {
        // 409 is Stripe saying a request under this key is still in flight — so a charge may be
        // being created right now. Calling any of these a decline would have the platform tell a
        // member their payment failed while Stripe was taking the money.
        var handler = new StripeStub((_, _) => (status, """{"error":{"message":"try again"}}"""));

        var result = await Gateway(handler).AuthorizeAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.Retryable);
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task An_unreachable_stripe_is_never_reported_as_a_decline()
    {
        var handler = new StripeStub((_, _) => (HttpStatusCode.OK, ""))
        {
            Throw = new HttpRequestException("dns"),
        };

        var result = await Gateway(handler).AuthorizeAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task An_unreachable_stripe_during_session_resolution_is_also_not_a_decline()
    {
        var handler = new StripeStub((_, _) => (HttpStatusCode.OK, ""))
        {
            Throw = new HttpRequestException("dns"),
        };

        var result = await Gateway(handler).CaptureAsync(
            Request() with { ExternalReference = "cs_test_1" });

        result.Outcome.Should().Be(GatewayOutcome.Unavailable);
    }

    [Fact]
    public async Task A_cancellation_the_caller_asked_for_is_not_swallowed_as_a_gateway_failure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var handler = StripeStub.Checkout();

        var act = async () => await Gateway(handler).AuthorizeAsync(Request(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── What it tells the platform about itself ──────────────────────────────

    [Fact]
    public void It_declares_cards_and_separate_authorisation()
    {
        // The capabilities endpoint builds `supportedMethods` from exactly this, and the wallet's
        // deposit button is drawn from that. An adapter that declared nothing would register and
        // still leave every deposit screen saying the platform cannot take payments.
        var gateway = Gateway(StripeStub.Checkout());

        gateway.SupportedMethods.Should().Contain(PaymentMethodKind.Card);
        gateway.SupportsSeparateAuthorization.Should().BeTrue();

        // Empty means "does not restrict". The account's own country and agreement decide, and this
        // adapter is not entitled to assert a list on its behalf.
        gateway.SupportedCurrencies.Should().BeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Intent(string status) =>
        $$"""{"id":"pi_123","status":"{{status}}"}""";

    private static StripePaymentGateway Gateway(StripeStub handler) =>
        new(
            new StaticStripeOptions(new StripeOptions
            {
                SecretKey = "sk_test_stub",
                ApiBase = "https://stripe.test",
                CheckoutSuccessUrl = "https://diaspora.test/wallet/paid",
                CheckoutCancelUrl = "https://diaspora.test/wallet/cancelled",
            }),
            new HttpClient(handler) { BaseAddress = new Uri("https://stripe.test") },
            NullLogger<StripePaymentGateway>.Instance);

    private static PaymentGatewayRequest Request() =>
        new()
        {
            PaymentIntentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Amount = Money.FromMinorUnits(1_000, "GBP"),
            MethodKind = PaymentMethodKind.Card,
            Description = "Adding money to your wallet",
        };

    private sealed class StaticStripeOptions(StripeOptions value) : IOptionsMonitor<StripeOptions>
    {
        public StripeOptions CurrentValue => value;

        public StripeOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<StripeOptions, string?> listener) => null;
    }

    /// <summary>
    /// A transport that answers per request, because the Checkout flow makes two calls: resolve the
    /// session, then act on the intent. A single canned response could not tell those apart, and a
    /// test that could not tell them apart would pass whichever one the adapter got wrong.
    /// </summary>
    private sealed class StripeStub(
        Func<HttpRequestMessage, int, (HttpStatusCode Status, string Body)> responder)
        : HttpMessageHandler
    {
        private int _calls;

        public bool Called => _calls > 0;

        public List<string> Paths { get; } = [];

        public string? LastPath => Paths.Count > 0 ? Paths[^1] : null;

        public string? IdempotencyKey { get; private set; }

        /// <summary>The form of the LAST request, which is the one that acted.</summary>
        public Dictionary<string, string> Form { get; } = new(StringComparer.Ordinal);

        public Exception? Throw { get; init; }

        /// <summary>Opening a Checkout: one call, answered with a session and its hosted URL.</summary>
        public static StripeStub Checkout() =>
            new((_, _) => (
                HttpStatusCode.OK,
                """{"id":"cs_test_1","url":"https://checkout.stripe.com/c/pay/cs_test_1"}"""));

        /// <summary>A completed session resolving to pi_123, then whatever the operation returns.</summary>
        public static StripeStub CompletedSessionThen(string body) =>
            new((_, call) => call == 0
                ? (HttpStatusCode.OK, """{"id":"cs_test_1","payment_intent":"pi_123"}""")
                : (HttpStatusCode.OK, body));

        public static StripeStub Always(string body) =>
            new((_, _) => (HttpStatusCode.OK, body));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var call = _calls++;
            Paths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);

            if (request.Headers.TryGetValues("Idempotency-Key", out var key))
            {
                IdempotencyKey = string.Join(",", key);
            }

            if (request.Content is not null)
            {
                Form.Clear();
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

            var (status, body) = responder(request, call);

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}

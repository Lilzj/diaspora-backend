using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Payments.PayPal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// PayPal's payout callbacks.
///
/// <para>
/// <b>For PayPal this is the only route to a settled withdrawal, not the fast one.</b>
/// <c>ISettlementStatusProvider</c> resolves a stuck payout by asking, and PayPal cannot be asked:
/// the resolution sweep only ever sees withdrawals marked <c>Processing</c> with a null settlement
/// reference — that is what the case *is* — and PayPal offers no lookup by <c>sender_batch_id</c>,
/// only by the <c>payout_batch_id</c> from the response that went missing. Resubmitting the batch to
/// read it back would create the payout if the original never landed.
/// </para>
///
/// <para>
/// So these are not tests of a convenience. Everything a member is ever told about a PayPal payout
/// arrives through this class, which is why most of what follows is about what it refuses.
/// </para>
/// </summary>
public sealed class PayPalPayoutWebhookTests
{
    // ── What it refuses ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_callback_is_unverified_when_no_webhook_id_is_configured()
    {
        // Refuses rather than waves through. A deployment that has not configured the webhook id
        // has no way to tell PayPal's callbacks from anybody else's, and "somebody was paid" is
        // exactly the message an attacker would send.
        var result = await Processor(webhookId: string.Empty)
            .ProcessAsync(Envelope(Succeeded(), WithHeaders()));

        result.IsVerified.Should().BeFalse();
        result.IsRecognized.Should().BeFalse();
    }

    [Theory]
    [InlineData("paypal-transmission-id")]
    [InlineData("paypal-transmission-time")]
    [InlineData("paypal-transmission-sig")]
    [InlineData("paypal-cert-url")]
    [InlineData("paypal-auth-algo")]
    public async Task A_callback_missing_any_transmission_header_is_unverified(string missing)
    {
        // Each header is part of the question PayPal is asked. A missing one cannot be guessed, and
        // a request built without it would be asking about a different transmission.
        var headers = WithHeaders();
        headers.Remove(missing);

        var result = await Processor().ProcessAsync(Envelope(Succeeded(), headers));

        result.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task A_callback_is_unverified_when_paypal_cannot_be_reached()
    {
        // Unreachable is unverified, never "probably fine". The sweep is what settles the
        // withdrawal instead, a little later.
        var result = await Processor(transport: new UnreachableHandler())
            .ProcessAsync(Envelope(Succeeded(), WithHeaders()));

        result.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task A_callback_paypal_does_not_confirm_is_unverified()
    {
        var result = await Processor(
                transport: new StubHandler("""{"verification_status":"FAILURE"}"""))
            .ProcessAsync(Envelope(Succeeded(), WithHeaders()));

        result.IsVerified.Should().BeFalse();
    }

    // ── What it reads ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_succeeded_item_names_the_withdrawal_it_is_about()
    {
        // **sender_item_id, not the batch.** The settlement adapter sets it to the instruction's own
        // reference, and PayPal's docs say the PAYOUTSBATCH events carry no item information — so
        // the item events are both the useful ones and the ones carrying our identifier.
        var result = await Processor().ProcessAsync(Envelope(Succeeded(), WithHeaders()));

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeTrue();
        result.EventKind.Should().Be("paid");
        result.WithdrawalReference.Should().Be("withdrawal-abc");
        result.ProviderReference.Should().Be("item-123");
    }

    [Theory]
    [InlineData("PAYMENT.PAYOUTS-ITEM.FAILED", "failed")]
    [InlineData("PAYMENT.PAYOUTS-ITEM.BLOCKED", "failed")]
    [InlineData("PAYMENT.PAYOUTS-ITEM.CANCELED", "failed")]
    [InlineData("PAYMENT.PAYOUTS-ITEM.RETURNED", "reversed")]
    [InlineData("PAYMENT.PAYOUTS-ITEM.REFUNDED", "reversed")]
    public async Task Each_outcome_maps_onto_the_platforms_three_words(string eventType, string kind)
    {
        var result = await Processor().ProcessAsync(Envelope(Event(eventType), WithHeaders()));

        result.EventKind.Should().Be(kind);
    }

    // ── What it deliberately does not act on ────────────────────────────────────────────────

    [Theory]
    [InlineData("PAYMENT.PAYOUTS-ITEM.UNCLAIMED")]
    [InlineData("PAYMENT.PAYOUTS-ITEM.HELD")]
    public async Task An_item_that_has_not_finished_moving_is_not_acted_on(string eventType)
    {
        // The money has left neither PayPal nor this platform's hold. An unclaimed payout becomes
        // RETURNED after thirty days if nobody claims it, so calling it failed now would release a
        // hold while PayPal still has the money committed — and calling it paid would be worse.
        var result = await Processor().ProcessAsync(Envelope(Event(eventType), WithHeaders()));

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeFalse();
        result.EventKind.Should().BeNull();
    }

    [Fact]
    public async Task A_batch_level_event_is_not_acted_on()
    {
        // PayPal's own documentation: the PAYOUTSBATCH webhooks "do not contain item-related
        // information". There is nothing on one to name a withdrawal with.
        var result = await Processor()
            .ProcessAsync(Envelope(Event("PAYMENT.PAYOUTSBATCH.SUCCESS"), WithHeaders()));

        result.IsRecognized.Should().BeFalse();
    }

    [Fact]
    public async Task An_event_type_nobody_has_taught_it_is_not_acted_on()
    {
        // PayPal's vocabulary is wider than this platform's and grows. A default that guessed
        // "paid" would pay on an event that meant the opposite.
        var result = await Processor()
            .ProcessAsync(Envelope(Event("PAYMENT.PAYOUTS-ITEM.SOMETHING-NEW"), WithHeaders()));

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeFalse();
    }

    [Fact]
    public async Task A_verified_body_that_is_not_json_is_not_acted_on()
    {
        var result = await Processor().ProcessAsync(Envelope("not json at all", WithHeaders()));

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeFalse();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static PayPalPayoutWebhookProcessor Processor(
        string webhookId = "WH-TEST", HttpMessageHandler? transport = null)
    {
        var options = new StaticOptions(new PayPalOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            ApiBase = PayPalOptions.SandboxApiBase,
            WebhookId = webhookId,
        });

        // The token cache is given a handler that always issues one, so these tests are about
        // verification rather than about authentication.
        var tokens = new PayPalAccessTokenCache(
            options,
            new HttpClient(new StubHandler("""{"access_token":"tok","expires_in":3600}""")),
            NullLogger<PayPalAccessTokenCache>.Instance);

        return new PayPalPayoutWebhookProcessor(
            options,
            tokens,
            new HttpClient(transport ?? new StubHandler("""{"verification_status":"SUCCESS"}""")),
            NullLogger<PayPalPayoutWebhookProcessor>.Instance);
    }

    private static Dictionary<string, string> WithHeaders() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["paypal-transmission-id"] = "t-1",
        ["paypal-transmission-time"] = "2026-09-08T00:00:00Z",
        ["paypal-transmission-sig"] = "sig",
        ["paypal-cert-url"] = "https://api.sandbox.paypal.com/cert.pem",
        ["paypal-auth-algo"] = "SHA256withRSA",
    };

    private static PayoutWebhookEnvelope Envelope(
        string body, IReadOnlyDictionary<string, string> headers) => new()
    {
        Provider = "paypal",
        RawBody = body,
        Headers = headers,
        ReceivedAt = new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero),
    };

    private static string Succeeded() => Event("PAYMENT.PAYOUTS-ITEM.SUCCEEDED");

    private static string Event(string eventType) =>
        $$"""
        {
          "event_type": "{{eventType}}",
          "resource": {
            "payout_item_id": "item-123",
            "payout_batch_id": "batch-456",
            "payout_item": { "sender_item_id": "withdrawal-abc" }
          }
        }
        """;

    private sealed class StaticOptions(PayPalOptions value) : IOptionsMonitor<PayPalOptions>
    {
        public PayPalOptions CurrentValue => value;

        public PayPalOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<PayPalOptions, string?> listener) => null;
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("unreachable");
    }
}

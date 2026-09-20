using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Modules.Financial.Infrastructure.Gateways;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The provider's own voice — the surface B15 documented and never mapped.
/// <para>
/// <b>Until this route existed, nothing but a client call could confirm a payment.</b> The processor
/// was written, registered and unit-testable; its own summary said "the endpoint refuses anything
/// whose <c>IsVerified</c> is false", and there was no endpoint. These tests are that sentence made
/// executable.
/// </para>
/// <para>
/// <b>Four gates, and each one has a test.</b> A registered processor for the named provider; a
/// signature that verifies over the raw body; an event identifier not seen before; and a transition
/// the aggregate allows.
/// </para>
/// </summary>
public sealed class FinancialWebhookTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FinancialWebhookTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    /// <summary>
    /// <b>The most security-sensitive branch in the module.</b> An unsigned body is an
    /// unauthenticated request that would otherwise move money, and it is refused before a single
    /// field of the payload is read.
    /// </summary>
    [Fact]
    public async Task An_unsigned_callback_is_refused_and_nothing_is_recorded()
    {
        var body = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-unsigned-1");

        var response = await PostAsync(body, signature: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // Not recorded, deliberately: anybody can post an unsigned body, and a table anybody can
        // write a row to is a table anybody can fill.
        (await context.Set<ProcessedWebhookEvent>()
                .AnyAsync(record => record.ProviderEventId == "evt-unsigned-1"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task A_callback_signed_with_the_wrong_secret_is_refused()
    {
        var body = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-wrong-secret");

        var response = await PostAsync(
            body, SimulatedPaymentWebhookProcessor.Sign(body, "not-the-secret"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// <b>The signature is over the bytes that arrived, not over a re-serialised object.</b> A body
    /// altered after signing verifies against nothing, which is what stops a caller replaying one
    /// intent's signature against another's amount.
    /// </summary>
    [Fact]
    public async Task A_tampered_body_no_longer_verifies_against_its_own_signature()
    {
        var original = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-tampered");
        var signature = SimulatedPaymentWebhookProcessor.Sign(original);
        var tampered = original.Replace("\"amountMinorUnits\":1000", "\"amountMinorUnits\":100000",
            StringComparison.Ordinal);

        var response = await PostAsync(tampered, signature);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_callback_for_an_unregistered_provider_is_a_404()
    {
        var body = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-unknown-provider");

        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/financial/webhooks/stripe", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            SimulatedPaymentWebhookProcessor.SignatureHeader,
            SimulatedPaymentWebhookProcessor.Sign(body));

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <b>A callback about a payment this deployment has never heard of is a success, not an
    /// error.</b> Answering 4xx buys an unbounded redelivery loop against a reference that will never
    /// resolve — and the callback is still recorded, because "we received something we could not
    /// match" is a fact support needs.
    /// </summary>
    [Fact]
    public async Task A_verified_callback_for_an_unknown_payment_is_accepted_and_recorded()
    {
        var body = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-unmatched-1");

        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Unmatched));
        acknowledgement.WasDuplicate.Should().BeFalse();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await context.Set<ProcessedWebhookEvent>()
                .AnyAsync(record => record.ProviderEventId == "evt-unmatched-1"))
            .Should().BeTrue("an unmatched callback is a fact support needs");
    }

    /// <summary>
    /// <b>The replay barrier, which is a database constraint rather than a handler check.</b> A
    /// provider that has not had a 2xx within its timeout redelivers, sometimes for days; the second
    /// delivery is a lookup rather than a second capture.
    /// </summary>
    [Fact]
    public async Task A_redelivered_callback_replays_its_stored_outcome_and_does_nothing()
    {
        var body = Body("captured", Guid.NewGuid().ToString(), eventId: "evt-replay-1");
        var signature = SimulatedPaymentWebhookProcessor.Sign(body);

        var first = await PostAsync(body, signature);
        var second = await PostAsync(body, signature);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var replayed = await ReadDataAsync<AcknowledgementShape>(second);

        replayed.WasDuplicate.Should().BeTrue();
        replayed.Outcome.Should().Be(nameof(WebhookOutcome.Unmatched));

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await context.Set<ProcessedWebhookEvent>()
                .CountAsync(record => record.ProviderEventId == "evt-replay-1"))
            .Should().Be(1, "one row per provider event, refused by a unique index");
    }

    /// <summary>
    /// A provider that sends no event identifier still gets replay protection, from a hash of the
    /// body it did send. Weaker than a real event id and strictly better than nothing.
    /// </summary>
    [Fact]
    public async Task A_callback_with_no_event_identifier_is_de_duplicated_by_its_body()
    {
        var reference = Guid.NewGuid().ToString();
        var body = Body("captured", reference, eventId: null);
        var signature = SimulatedPaymentWebhookProcessor.Sign(body);

        await PostAsync(body, signature);

        var second = await PostAsync(body, signature);
        var replayed = await ReadDataAsync<AcknowledgementShape>(second);

        replayed.WasDuplicate.Should().BeTrue();
    }

    /// <summary>
    /// <b>The whole point of the route: a provider's word captures a payment, and the ledger moves.</b>
    /// The intent is created through the ordinary member route, so this is the real path rather than
    /// a hand-staged aggregate.
    /// </summary>
    [Fact]
    public async Task A_verified_capture_callback_captures_the_payment_it_names()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 12.50m);

        // Authorised first: a capture on an intent no gateway has ever seen is one the state machine
        // is right to refuse. Through the member's route rather than by callback, because an
        // authorised callback takes the payment itself (the next test), and this one is about a
        // provider that took the money on its own side and says so.
        await AuthorizeThroughTheMemberRouteAsync(intentId);

        var body = Body("captured", intentId.ToString(), "evt-capture-1", amountMinorUnits: 1250);
        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Applied));

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(
            PaymentIntentId.From(intentId));

        intent!.Status.Should().Be(PaymentIntentStatus.Captured);
        intent.CapturedMinorUnits.Should().Be(1250);
    }

    /// <summary>
    /// <b>A capture the provider refuses gives the member their money back, and is recorded as
    /// its own kind of thing.</b>
    /// <para>
    /// An authorisation callback authorises the intent and then asks the provider to take the
    /// money. When that second call is refused the payment is marked <c>Failed</c> — which is
    /// honest, it did fail — and the member is told so. What the provider was never told is to
    /// stop holding their funds: <c>CancelAsync</c> has been on the gateway contract since the
    /// module shipped and had no caller anywhere in it. The hold stayed until the provider's own
    /// authorisation lapsed, days later, with the member short the money and this platform
    /// believing the matter closed.
    /// </para>
    /// <para>
    /// It was also filed as <c>Refused</c>, beside replays, wrong currencies and transitions the
    /// aggregate does not allow — every one of which means nothing was owed and nothing was taken.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_refused_capture_fails_the_payment_and_is_recorded_as_its_own_thing()
    {
        await AuthenticateAsync();

        // The simulator decides by the amount's minor units: a suffix of 01 is declined. Only the
        // capture reaches it here, because the authorisation on this path is the aggregate's own.
        var intentId = await CreatePaymentIntentAsync(amount: 12.01m);

        var body = Body("authorized", intentId.ToString(), "evt-capture-refused-1", amountMinorUnits: 1201);
        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the callback itself was right");

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(
            nameof(WebhookOutcome.CaptureFailed),
            "a capture this platform could not complete is not a callback it refused");

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(PaymentIntentId.From(intentId));

        intent!.Status.Should().Be(PaymentIntentStatus.Failed, "the payment did not go through");

        // And nothing claims the money arrived. A capture recorded here would be a ledger
        // asserting funds the provider never released, which is worse than not capturing at all.
        intent.CapturedMinorUnits.Should().Be(0);

        // The half the aggregate cannot show. `Failed` looks the same whether or not the provider
        // was told to stop holding the member's money, and it was not told for the module's whole
        // life — every adapter implemented `CancelAsync` and nothing called it.
        SimulatedPaymentGateway.Voided.Should().Contain(
            intentId, "a payment that did not go through must not leave the member's funds held");
    }

    /// <summary>
    /// <b>An authorised callback takes the payment, and the provider's later word on it moves nothing
    /// twice.</b>
    /// <para>
    /// Hosted pages authorise and hold, and nothing else on the platform would ever capture what they
    /// hold: on 6 Sep 2026 a member's £50 sat authorised with no ledger entry. The handler has taken
    /// the payment on an authorised callback since then, and no test said so. The capture test above
    /// staged its authorisation with a callback, met a payment that was already captured, and failed
    /// from that day until 17 Sep 2026.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_verified_authorisation_callback_takes_the_payment_once()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 12.50m);

        var authorized = Body("authorized", intentId.ToString(), "evt-take-1", amountMinorUnits: 1250);
        var taken = await ReadDataAsync<AcknowledgementShape>(
            await PostAsync(authorized, SimulatedPaymentWebhookProcessor.Sign(authorized)));

        taken.Outcome.Should().Be(nameof(WebhookOutcome.Applied));

        // The provider then reports the capture the platform asked it for. The payment is already
        // captured, so the state machine refuses the report and the money is not counted again.
        var captured = Body("captured", intentId.ToString(), "evt-take-2", amountMinorUnits: 1250);
        var reported = await ReadDataAsync<AcknowledgementShape>(
            await PostAsync(captured, SimulatedPaymentWebhookProcessor.Sign(captured)));

        reported.Outcome.Should().Be(nameof(WebhookOutcome.Refused));

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(
            PaymentIntentId.From(intentId));

        intent!.Status.Should().Be(PaymentIntentStatus.Captured);
        intent.CapturedMinorUnits.Should().Be(1250, "money moves once per payment");
    }

    /// <summary>
    /// <b>A second capture callback for one intent is refused by the state machine, not applied.</b>
    /// Two independent barriers stand behind this — the aggregate's transition guard and the ledger's
    /// reference idempotency — because this is the path where a duplicate costs real money.
    /// </summary>
    [Fact]
    public async Task A_second_capture_callback_for_one_payment_is_refused_rather_than_applied()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 7m);

        await AuthorizeThroughTheMemberRouteAsync(intentId);

        var first = Body("captured", intentId.ToString(), "evt-double-1", amountMinorUnits: 700);
        var applied = await ReadDataAsync<AcknowledgementShape>(
            await PostAsync(first, SimulatedPaymentWebhookProcessor.Sign(first)));

        // Without this the test passes for the wrong reason, as it did while an authorised
        // callback staged the payment: nothing was captured by the "first" capture at all.
        applied.Outcome.Should().Be(nameof(WebhookOutcome.Applied));

        // A different event id, so the replay barrier does not catch it. Only the state machine can.
        var second = Body("captured", intentId.ToString(), "evt-double-2", amountMinorUnits: 700);
        var response = await PostAsync(second, SimulatedPaymentWebhookProcessor.Sign(second));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Refused));

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(
            PaymentIntentId.From(intentId));

        intent!.CapturedMinorUnits.Should().Be(700, "money moves once per payment");
    }

    /// <summary>
    /// <b>A capture for more than was authorised is refused.</b> The provider says what happened; the
    /// aggregate decides whether that is legal, and an amount larger than the intent is not.
    /// </summary>
    [Fact]
    public async Task A_capture_callback_for_more_than_the_intent_is_refused()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 5m);

        await AuthorizeThroughTheMemberRouteAsync(intentId);

        var body = Body("captured", intentId.ToString(), "evt-over-1", amountMinorUnits: 500_000);
        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Refused));

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(
            PaymentIntentId.From(intentId));

        intent!.Status.Should().Be(PaymentIntentStatus.Authorized, "an over-capture moves nothing");
    }

    /// <summary>
    /// A callback naming a different currency from the intent it claims to be about is a provider
    /// defect or a replay, and the platform does not convert — so there is no reading of it that is
    /// safe to act on.
    /// </summary>
    [Fact]
    public async Task A_callback_in_another_currency_is_refused()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 5m);

        var body = Body(
            "captured", intentId.ToString(), "evt-currency-1", amountMinorUnits: 500, currency: "USD");

        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));
        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Refused));
    }

    /// <summary>
    /// A verified callback whose kind means nothing to this platform is ignored rather than retried
    /// for ever — recognised as ours and recorded as having done nothing.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_event_kind_is_recorded_and_does_nothing()
    {
        await AuthenticateAsync();

        var intentId = await CreatePaymentIntentAsync(amount: 3m);

        var body = Body("disputed", intentId.ToString(), "evt-unknown-kind-1");
        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Refused));

        using var scope = Factory.Services.CreateScope();
        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();

        var intent = await intents.FindAsync(
            PaymentIntentId.From(intentId));

        intent!.Status.Should().Be(PaymentIntentStatus.Draft, "an unrecognised kind moves nothing");
    }

    /// <summary>
    /// A correctly signed body that will never parse is recognised as ours and ignored — retrying it
    /// for ever would be retrying something that cannot succeed.
    /// </summary>
    [Fact]
    public async Task A_signed_but_malformed_body_is_ignored_rather_than_retried()
    {
        const string body = "{not json at all";

        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var acknowledgement = await ReadDataAsync<AcknowledgementShape>(response);

        acknowledgement.Outcome.Should().Be(nameof(WebhookOutcome.Ignored));
    }

    /// <summary>
    /// The route is anonymous by design — a provider holds no bearer token — and the signature is the
    /// credential. This asserts the design rather than assuming it.
    /// </summary>
    [Fact]
    public async Task The_webhook_route_needs_no_bearer_token()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;

        var body = Body("captured", Guid.NewGuid().ToString(), "evt-anonymous-1");
        var response = await PostAsync(body, SimulatedPaymentWebhookProcessor.Sign(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static string Body(
        string eventKind,
        string externalReference,
        string? eventId,
        long amountMinorUnits = 1000,
        string currency = "GBP")
    {
        var id = eventId is null ? string.Empty : $"\"eventId\":\"{eventId}\",";

        return "{"
            + $"\"eventKind\":\"{eventKind}\","
            + $"\"externalReference\":\"{externalReference}\","
            + id
            + $"\"amountMinorUnits\":{amountMinorUnits},"
            + $"\"currency\":\"{currency}\","
            + "\"status\":\"succeeded\"}";
    }

    private async Task<HttpResponseMessage> PostAsync(string body, string? signature)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                $"/api/v1/financial/webhooks/{SimulatedPaymentGateway.ProviderName}",
                UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (signature is not null)
        {
            request.Headers.Add(SimulatedPaymentWebhookProcessor.SignatureHeader, signature);
        }

        return await HttpClient.SendAsync(request);
    }

    private async Task<Guid> CreatePaymentIntentAsync(decimal amount)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/payment-intents", UriKind.Relative),
            new
            {
                amount,
                currency = "GBP",
                methodKind = nameof(PaymentMethodKind.Card),
                description = "A webhook integration test",
                idempotencyKey = Guid.NewGuid().ToString("N")
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var intent = await ReadDataAsync<PaymentShape>(response);

        return intent.PaymentIntentId;
    }

    /// <summary>
    /// Authorises a payment the way a member's card does: through the gateway, which holds the money
    /// and leaves the payment <c>Authorized</c> until something takes it.
    /// <para>
    /// Not by callback. An authorised callback takes the payment as well, so a test that needs an
    /// authorised payment to act on cannot stage it that way.
    /// </para>
    /// </summary>
    private async Task AuthorizeThroughTheMemberRouteAsync(Guid intentId)
    {
        var response = await HttpClient.PostAsync(
            new Uri($"/api/v1/financial/payment-intents/{intentId}/authorize", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadDataAsync<PaymentShape>(response)).Status
            .Should().Be(nameof(PaymentIntentStatus.Authorized));
    }

    /// <summary>
    /// A member at the Verified tier — the rung every money-moving capability opens at, and the only
    /// one from which a payment intent can be created at all.
    /// </summary>
    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record AcknowledgementShape(string Outcome, bool WasDuplicate, string? Detail);

    private sealed record PaymentShape(Guid PaymentIntentId, string Status, string Purpose);
}

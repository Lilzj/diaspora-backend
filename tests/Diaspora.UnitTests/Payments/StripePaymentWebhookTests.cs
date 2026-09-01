using Diaspora.Payments.Stripe;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// Translating a verified Stripe charge callback.
/// <para>
/// <b>Correlation is the whole difficulty, and it is easy to get subtly wrong.</b> A Checkout
/// session has no Stripe PaymentIntent id until the member pays, so what the platform stored at
/// authorisation is a <c>cs_…</c> that no <c>payment_intent.*</c> event ever mentions. The session
/// events carry <c>client_reference_id</c> — the platform's own payment id — and
/// <c>ProcessPaymentWebhookCommandHandler</c> falls back to finding an intent by that when the
/// external reference does not resolve. Prefer <c>payment_intent</c> over it and correlation stops
/// working the moment a session completes, which is precisely when it matters.
/// </para>
/// <para>
/// Verification is exercised separately; these drive <c>Translate</c> directly, so a signature is
/// not needed and neither half can accidentally stand in for the other.
/// </para>
/// </summary>
public class StripePaymentWebhookTests
{
    private const string PlatformPaymentId = "11111111111111111111111111111111";

    [Fact]
    public void A_completed_checkout_is_answered_with_the_platform_payment_id()
    {
        var result = StripePaymentWebhookProcessor.Translate($$"""
        {
          "id": "evt_1",
          "type": "checkout.session.completed",
          "data": { "object": {
            "id": "cs_test_1",
            "client_reference_id": "{{PlatformPaymentId}}",
            "payment_intent": "pi_123",
            "currency": "gbp",
            "amount_total": 1000,
            "payment_status": "unpaid",
            "status": "complete"
          } }
        }
        """);

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeTrue();

        // Not pi_123. The platform stored cs_test_1, so the intent is found by the platform id.
        result.ExternalReference.Should().Be(PlatformPaymentId);
    }

    [Fact]
    public void A_completed_checkout_means_authorised_not_paid()
    {
        // The session was created with capture_method=manual, so the money is held and
        // payment_status stays unpaid until the platform captures. Reading this as "succeeded"
        // would have the platform post a completed payment for money it has not taken.
        var result = StripePaymentWebhookProcessor.Translate($$"""
        {
          "id": "evt_1",
          "type": "checkout.session.completed",
          "data": { "object": {
            "id": "cs_test_1",
            "client_reference_id": "{{PlatformPaymentId}}",
            "amount_total": 2500,
            "currency": "gbp"
          } }
        }
        """);

        result.EventKind.Should().Be("authorized");
        result.AmountMinorUnits.Should().Be(2500);
        result.Currency.Should().Be("GBP");
    }

    [Fact]
    public void An_expired_checkout_cancels_the_payment()
    {
        var result = StripePaymentWebhookProcessor.Translate($$"""
        {
          "id": "evt_2",
          "type": "checkout.session.expired",
          "data": { "object": {
            "id": "cs_test_1",
            "client_reference_id": "{{PlatformPaymentId}}",
            "amount_total": 1000
          } }
        }
        """);

        result.EventKind.Should().Be("canceled");
        result.ExternalReference.Should().Be(PlatformPaymentId);
    }

    [Fact]
    public void An_amount_capturable_update_still_reports_the_capturable_amount()
    {
        // The other shape that means "authorised". It has amount_capturable and no amount_total.
        var result = StripePaymentWebhookProcessor.Translate("""
        {
          "id": "evt_3",
          "type": "payment_intent.amount_capturable_updated",
          "data": { "object": {
            "id": "pi_123",
            "amount": 5000,
            "amount_capturable": 4000,
            "currency": "gbp"
          } }
        }
        """);

        result.EventKind.Should().Be("authorized");
        result.AmountMinorUnits.Should().Be(4000, "capturable is what may actually be taken");
        result.ExternalReference.Should().Be("pi_123");
    }

    [Fact]
    public void A_refund_reports_the_amount_refunded_rather_than_the_original_charge()
    {
        // Reporting the original charge here would call a partial refund a full one.
        var result = StripePaymentWebhookProcessor.Translate("""
        {
          "id": "evt_4",
          "type": "charge.refunded",
          "data": { "object": {
            "id": "ch_1",
            "payment_intent": "pi_123",
            "amount": 5000,
            "amount_refunded": 1500,
            "currency": "gbp"
          } }
        }
        """);

        result.EventKind.Should().Be("refunded");
        result.AmountMinorUnits.Should().Be(1500);
        result.ExternalReference.Should().Be("pi_123", "a charge event names its intent");
    }

    [Fact]
    public void A_failed_payment_carries_the_reason_the_member_can_act_on()
    {
        var result = StripePaymentWebhookProcessor.Translate("""
        {
          "id": "evt_5",
          "type": "payment_intent.payment_failed",
          "data": { "object": {
            "id": "pi_123",
            "amount": 1000,
            "currency": "gbp",
            "status": "requires_payment_method",
            "last_payment_error": { "code": "card_declined", "message": "Your card was declined." }
          } }
        }
        """);

        result.EventKind.Should().Be("failed");
        result.FailureCode.Should().Be("card_declined");
        result.FailureMessage.Should().Be("Your card was declined.");
    }

    [Fact]
    public void An_event_this_adapter_does_not_model_is_ignored_rather_than_guessed_at()
    {
        // A deployment may have subscribed to more types than these. Treating an unknown one as a
        // failure would fail payments that had in fact succeeded.
        var result = StripePaymentWebhookProcessor.Translate("""
        {
          "id": "evt_6",
          "type": "customer.subscription.updated",
          "data": { "object": { "id": "sub_1" } }
        }
        """);

        result.IsVerified.Should().BeTrue("it was a genuine Stripe event");
        result.IsRecognized.Should().BeFalse();
        result.EventId.Should().Be("evt_6", "an ignored event is still traceable");
    }

    [Fact]
    public void An_unparseable_body_is_ignored_rather_than_thrown()
    {
        var result = StripePaymentWebhookProcessor.Translate("not json");

        result.IsRecognized.Should().BeFalse();
    }

    [Fact]
    public void An_event_with_nothing_to_correlate_on_is_ignored()
    {
        var result = StripePaymentWebhookProcessor.Translate("""
        {
          "id": "evt_7",
          "type": "payment_intent.succeeded",
          "data": { "object": { "amount": 1000 } }
        }
        """);

        result.IsRecognized.Should().BeFalse("there is no reference to find an intent by");
        result.EventId.Should().Be("evt_7");
    }
}

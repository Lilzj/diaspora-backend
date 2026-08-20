using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The payment lifecycle.
/// <para>
/// The refusals are the point. A payment that quietly went backwards is the defect nobody finds
/// until a reconciliation two months later.
/// </para>
/// </summary>
public class PaymentIntentTests
{
    [Fact]
    public void A_new_intent_is_a_draft_and_announces_itself()
    {
        var intent = Intent();

        intent.Status.Should().Be(PaymentIntentStatus.Draft);
        intent.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_payment_for_nothing_is_refused()
    {
        var create = () => Intent(0m);

        create.Should().Throw<DomainException>().WithMessage("*Grant credit instead*");
    }

    [Fact]
    public void The_lifecycle_runs_draft_to_pending_to_authorized_to_captured_to_settled()
    {
        var intent = Intent();

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Status.Should().Be(PaymentIntentStatus.Pending);

        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);
        intent.Status.Should().Be(PaymentIntentStatus.Authorized);

        intent.Capture(intent.Amount, FinancialTestData.Now);
        intent.Status.Should().Be(PaymentIntentStatus.Captured);

        intent.Settle(FinancialTestData.Now, "settled");
        intent.Status.Should().Be(PaymentIntentStatus.Settled);
    }

    /// <summary>
    /// Several real methods have no separate authorisation step at all: a bank transfer and a
    /// mobile-money push either happened or did not.
    /// </summary>
    [Fact]
    public void A_capture_is_permitted_straight_from_pending()
    {
        var intent = Intent();

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Capture(intent.Amount, FinancialTestData.Now);

        intent.Status.Should().Be(PaymentIntentStatus.Captured);
    }

    [Fact]
    public void A_capture_larger_than_the_authorised_amount_is_refused()
    {
        var intent = Intent(25m);

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);

        var capture = () => intent.Capture(FinancialTestData.Gbp_(26m), FinancialTestData.Now);

        capture.Should().Throw<DomainException>().WithMessage("*cannot exceed*");
    }

    /// <summary>A partial fulfilment is legitimate and common.</summary>
    [Fact]
    public void A_capture_smaller_than_the_authorised_amount_is_allowed()
    {
        var intent = Intent(25m);

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);
        intent.Capture(FinancialTestData.Gbp_(10m), FinancialTestData.Now);

        intent.CapturedMinorUnits.Should().Be(1000);
        intent.RefundableRemaining.MinorUnits.Should().Be(1000);
    }

    [Fact]
    public void A_capture_in_another_currency_is_refused()
    {
        var intent = Intent();

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);

        var capture = () => intent.Capture(
            Money.FromAmount(25m, "USD"), FinancialTestData.Now);

        capture.Should().Throw<DomainException>().WithMessage("*cannot settle*");
    }

    /// <summary>
    /// Cancelling a captured payment is a refund, and one method meaning both would give the
    /// platform two names for an operation whose accounting is entirely different.
    /// </summary>
    [Fact]
    public void A_captured_payment_cannot_be_cancelled()
    {
        var intent = Captured();

        var cancel = () => intent.Cancel(FinancialTestData.Now);

        cancel.Should().Throw<DomainException>().WithMessage("*Refund it*");
    }

    [Fact]
    public void An_uncaptured_payment_cannot_be_refunded()
    {
        var intent = Intent();

        var refund = () => intent.Refund(
            Guid.NewGuid(), intent.Amount, RefundReason.RequestedByPayer, FinancialTestData.Now);

        refund.Should().Throw<DomainException>().WithMessage("*Only a captured payment*");
    }

    [Fact]
    public void A_partial_refund_leaves_the_intent_captured()
    {
        var intent = Captured(25m);

        intent.Refund(
            Guid.NewGuid(),
            FinancialTestData.Gbp_(10m),
            RefundReason.RequestedByPayer,
            FinancialTestData.Now);

        intent.Status.Should().Be(PaymentIntentStatus.Captured);
        intent.RefundedMinorUnits.Should().Be(1000);
        intent.RefundableRemaining.MinorUnits.Should().Be(1500);
    }

    [Fact]
    public void A_full_refund_moves_the_intent_to_refunded()
    {
        var intent = Captured(25m);

        intent.Refund(
            Guid.NewGuid(), intent.Amount, RefundReason.Duplicate, FinancialTestData.Now);

        intent.Status.Should().Be(PaymentIntentStatus.Refunded);
        intent.RefundableRemaining.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Refunding_more_than_was_captured_is_refused()
    {
        var intent = Captured(25m);

        var refund = () => intent.Refund(
            Guid.NewGuid(),
            FinancialTestData.Gbp_(26m),
            RefundReason.Goodwill,
            FinancialTestData.Now);

        refund.Should().Throw<DomainException>().WithMessage("*larger than what remains*");
    }

    /// <summary>
    /// Three declines followed by a success is a different story from one clean success, and it is
    /// the story a fraud model, a support agent and a retry policy all need.
    /// </summary>
    [Fact]
    public void Every_attempt_is_kept_including_the_failures()
    {
        var intent = Intent();

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now, "simulated");
        intent.Fail(Guid.NewGuid(), "card_declined", "Declined.", false, FinancialTestData.Now);

        intent.Attempts.Should().HaveCount(2);
        intent.Attempts[^1].Status.Should().Be(PaymentAttemptStatus.Failed);
        intent.Attempts[^1].FailureCode.Should().Be("card_declined");
    }

    [Fact]
    public void A_challenge_keeps_the_intent_pending_and_carries_the_payload()
    {
        var intent = Intent();

        intent.RequireAction(Guid.NewGuid(), "challenge-token", FinancialTestData.Now);

        intent.Status.Should().Be(PaymentIntentStatus.Pending);
        intent.NextActionPayload.Should().Be("challenge-token");
    }

    [Fact]
    public void An_authorisation_clears_a_pending_challenge()
    {
        var intent = Intent();

        intent.RequireAction(Guid.NewGuid(), "challenge-token", FinancialTestData.Now);
        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);

        intent.NextActionPayload.Should().BeNull();
    }

    [Fact]
    public void An_intent_past_its_expiry_reports_that_it_has_lapsed()
    {
        var intent = Intent();

        intent.HasExpired(FinancialTestData.Now).Should().BeFalse();
        intent.HasExpired(FinancialTestData.Now.AddDays(8)).Should().BeTrue();
    }

    [Fact]
    public void A_cancelled_intent_stays_cancelled()
    {
        var intent = Intent();

        intent.Cancel(FinancialTestData.Now);
        intent.Cancel(FinancialTestData.Now, expired: true);

        intent.Status.Should().Be(PaymentIntentStatus.Cancelled);
    }

    private static PaymentIntent Intent(decimal amount = 25m) =>
        PaymentIntent.Create(
            PaymentIntentId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            FinancialTestData.Gbp_(amount),
            PaymentMethodKind.Card,
            "A test payment",
            FinancialTestData.Now);

    private static PaymentIntent Captured(decimal amount = 25m)
    {
        var intent = Intent(amount);

        intent.Submit(Guid.NewGuid(), FinancialTestData.Now);
        intent.Authorize(Guid.NewGuid(), Gateway(), FinancialTestData.Now);
        intent.Capture(intent.Amount, FinancialTestData.Now);

        return intent;
    }

    private static GatewayReference Gateway() =>
        GatewayReference.Create("simulated", "sim_ref_1", "succeeded");
}

using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// Turning a Stripe connected account into one answer about whether it can be paid.
/// <para>
/// <b>Several fields on a Stripe account look like they mean "this is fine" and only one does.</b>
/// <c>details_submitted</c> means the onboarding form was completed, which is the beginning of a
/// review rather than the end of one; <c>charges_enabled</c> is about taking money, not sending it.
/// The one that means Stripe will send money is <c>payouts_enabled</c>, and these pin that.
/// </para>
/// </summary>
public class StripeConnectVerificationTests
{
    [Fact]
    public void An_account_with_payouts_enabled_is_verified()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":true,"charges_enabled":true,
             "requirements":{"currently_due":[],"past_due":[]}}
            """);

        result.Status.Should().Be(PayoutProviderKycStatus.Verified);
        result.IsPayable.Should().BeTrue();
        result.Reference.Should().Be("acct_1");
    }

    /// <summary>
    /// An account may be payable today and still owe documents for a future threshold. Stripe having
    /// said it will send money is the operative fact.
    /// </summary>
    [Fact]
    public void Future_requirements_do_not_stop_an_account_that_can_be_paid_today()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":true,
             "requirements":{"currently_due":["individual.verification.document"]}}
            """);

        result.Status.Should().Be(PayoutProviderKycStatus.Verified);
        result.OutstandingRequirements.Should().Contain("individual.verification.document");
    }

    /// <summary>
    /// <b>Submitted is not verified.</b> Treating a completed form as a yes is how a payout is
    /// attempted against an account Stripe is still reviewing.
    /// </summary>
    [Fact]
    public void An_account_that_has_submitted_details_but_cannot_be_paid_is_pending()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":false,"charges_enabled":true,"details_submitted":true,
             "requirements":{"currently_due":["external_account"]}}
            """);

        result.Status.Should().Be(PayoutProviderKycStatus.Pending);
        result.IsPayable.Should().BeFalse();
        result.OutstandingRequirements.Should().Contain("external_account");
    }

    [Fact]
    public void An_account_with_nothing_outstanding_and_no_payouts_has_not_started()
    {
        var result = Map("""{"id":"acct_1","payouts_enabled":false,"requirements":{}}""");

        result.Status.Should().Be(PayoutProviderKycStatus.NotStarted);
    }

    [Theory]
    [InlineData("rejected.fraud", PayoutProviderKycStatus.Rejected)]
    [InlineData("rejected.terms_of_service", PayoutProviderKycStatus.Rejected)]
    [InlineData("requirements.past_due", PayoutProviderKycStatus.Restricted)]
    [InlineData("under_review", PayoutProviderKycStatus.Restricted)]
    public void A_disabled_account_is_classified_by_why_it_was_disabled(
        string reason, PayoutProviderKycStatus expected)
    {
        // A plain string rather than a raw literal: the JSON's own braces collide with raw-string
        // interpolation, and escaping them reads worse than escaping the quotes.
        var result = Map(
            "{\"id\":\"acct_1\",\"payouts_enabled\":false,"
            + "\"requirements\":{\"disabled_reason\":\"" + reason + "\"}}");

        result.Status.Should().Be(expected);
    }

    /// <summary>
    /// <b>An unrecognised reason is restricted, not fine.</b> Stripe's vocabulary changes as its
    /// compliance rules do, and an adapter that matched a fixed list would silently start reporting
    /// new states as payable.
    /// </summary>
    [Fact]
    public void A_disabled_reason_nobody_has_seen_before_still_refuses()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":false,
             "requirements":{"disabled_reason":"some.future.reason.invented.in.2027"}}
            """);

        result.Status.Should().Be(PayoutProviderKycStatus.Restricted);
        result.IsPayable.Should().BeFalse();
    }

    [Fact]
    public void A_deadline_is_carried_so_somebody_can_be_warned_before_it_passes()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":false,
             "requirements":{"currently_due":["document"],"current_deadline":1788000000}}
            """);

        result.ExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_788_000_000));
    }

    [Fact]
    public void An_unparseable_body_is_not_read_as_a_verified_account()
    {
        var result = Map("this is not json");

        result.Status.Should().Be(PayoutProviderKycStatus.NotStarted);
        result.IsPayable.Should().BeFalse();
    }

    [Fact]
    public void Past_due_and_currently_due_are_reported_together_without_duplicates()
    {
        var result = Map("""
            {"id":"acct_1","payouts_enabled":false,
             "requirements":{"currently_due":["a","b"],"past_due":["b","c"]}}
            """);

        result.OutstandingRequirements.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    private static PayoutProviderVerification Map(string payload) =>
        StripeConnectVerificationSource.Map("stripe", "acct_1", payload);
}

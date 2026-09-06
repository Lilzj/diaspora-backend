using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Options;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// What an earning is allowed to be, and what the configured rate does to a payment.
/// <para>
/// These are the rules a revenue dashboard is built on. Every one of them, wrong, produces a
/// plausible number rather than a failure — which is why they are pinned here rather than left to
/// be noticed by somebody reading their own income.
/// </para>
/// </summary>
public sealed class CreatorEarningTests
{
    private static readonly CreatorId Creator = CreatorId.From(Guid.NewGuid());
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static Result<CreatorEarning> Record(long share, long gross) =>
        CreatorEarning.Record(
            Creator, User, CreatorEarningSource.Donation, share, gross, "GBP", 2,
            "donation.completed:abc", "donation", Guid.NewGuid(), Now);

    // ── The share arithmetic ────────────────────────────────────────────────

    [Fact]
    public void A_donation_is_the_creators_in_full_by_default()
    {
        // The one rate seeded above zero, and the reason it is: a donation naming a beneficiary is
        // unambiguously theirs, and a platform cut nobody agreed would be invented here.
        var options = new CreatorRevenueShareOptions();

        options.ShareOf(5_000, CreatorEarningSource.Donation).Should().Be(5_000);
    }

    [Fact]
    public void Every_other_source_earns_nothing_until_somebody_sets_a_rate()
    {
        var options = new CreatorRevenueShareOptions();

        options.ShareOf(5_000, CreatorEarningSource.Sponsorship).Should().Be(0);
        options.ShareOf(5_000, CreatorEarningSource.Subscription).Should().Be(0);
        options.ShareOf(5_000, CreatorEarningSource.Referral).Should().Be(0);
    }

    [Fact]
    public void The_share_rounds_down()
    {
        // 33.33% of 10 pence is 3.333 pence. Rounding towards the creator would have the platform
        // paying out marginally more than it took, once per earning, for ever.
        var options = new CreatorRevenueShareOptions { SponsorshipBasisPoints = 3_333 };

        options.ShareOf(10, CreatorEarningSource.Sponsorship).Should().Be(3);
    }

    [Fact]
    public void A_zero_gross_earns_zero_rather_than_throwing()
    {
        var options = new CreatorRevenueShareOptions();

        options.ShareOf(0, CreatorEarningSource.Donation).Should().Be(0);
        options.ShareOf(-100, CreatorEarningSource.Donation).Should().Be(0);
    }

    // ── What the aggregate refuses ──────────────────────────────────────────

    [Fact]
    public void A_share_larger_than_the_payment_is_refused()
    {
        // Only a misconfigured rate produces this, and it is refused rather than clamped: clamping
        // would quietly pay the configured amount while the dashboard showed the paid one.
        Record(share: 6_000, gross: 5_000).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_zero_share_is_recorded_rather_than_dropped()
    {
        // The ordinary case for every source seeded at zero. Recording it is what lets a dashboard
        // show that something was attributed and carried no share, instead of showing nothing at all
        // and leaving a creator to wonder whether the platform noticed.
        var earning = Record(share: 0, gross: 5_000);

        earning.IsSuccess.Should().BeTrue();
        earning.Value.AmountMinorUnits.Should().Be(0);
        earning.Value.GrossMinorUnits.Should().Be(5_000);
    }

    [Fact]
    public void An_earning_needs_the_reference_of_the_event_that_produced_it()
    {
        // The idempotency barrier. Without it a redelivered event pays twice.
        CreatorEarning.Record(
            Creator, User, CreatorEarningSource.Donation, 100, 100, "GBP", 2,
            sourceReference: "  ", "donation", Guid.NewGuid(), Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_new_earning_is_pending_rather_than_available()
    {
        // The refund window. An earning available the instant a payment captures can be paid out and
        // then refunded, leaving the platform to recover money it has already sent.
        Record(share: 100, gross: 100).Value.State.Should().Be(CreatorEarningState.Pending);
    }

    // ── The lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public void An_earning_clears_then_pays_out_in_order()
    {
        var earning = Record(share: 100, gross: 100).Value;
        var payout = Guid.NewGuid();

        earning.MakeAvailable(Now).IsSuccess.Should().BeTrue();
        earning.State.Should().Be(CreatorEarningState.Available);

        earning.AttachToPayout(payout).IsSuccess.Should().BeTrue();
        earning.State.Should().Be(CreatorEarningState.Processing);
        earning.PayoutId.Should().Be(payout);

        earning.MarkPaid(Now).IsSuccess.Should().BeTrue();
        earning.State.Should().Be(CreatorEarningState.Paid);
    }

    [Fact]
    public void A_pending_earning_cannot_be_paid_out()
    {
        // Out of order is the whole risk: a payout naming an earning that has not cleared is money
        // leaving before the refund window closes.
        var earning = Record(share: 100, gross: 100).Value;

        earning.AttachToPayout(Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_refund_lands_even_after_the_payout_settled()
    {
        // Deliberately allowed from any state. A refund can arrive after a payout has settled, and
        // refusing to record it would leave the dashboard showing an earning the platform knows is
        // gone.
        var earning = Record(share: 100, gross: 100).Value;

        earning.MakeAvailable(Now);
        earning.AttachToPayout(Guid.NewGuid());
        earning.MarkPaid(Now);

        earning.Reverse(CreatorEarningState.Refunded, "The donor charged back.")
            .IsSuccess.Should().BeTrue();

        earning.State.Should().Be(CreatorEarningState.Refunded);
        earning.StateReason.Should().Be("The donor charged back.");
    }

    [Fact]
    public void A_reversal_must_be_a_terminal_state()
    {
        var earning = Record(share: 100, gross: 100).Value;

        earning.Reverse(CreatorEarningState.Available, null).IsFailure.Should().BeTrue();
    }

    // -- What a window may and may not narrow ---------------------------------

    [Fact]
    public void The_source_label_is_the_platforms_word_for_it()
    {
        // Served rather than held by a screen, so four surfaces cannot disagree about what
        // `Sponsorship` is called and so the wording is translatable in one place.
        CreatorEarningMapper.LabelFor(CreatorEarningSource.Sponsorship)
            .Should().Be("Sponsored content");

        CreatorEarningMapper.LabelFor(CreatorEarningSource.Donation).Should().Be("Donations");
    }

    [Fact]
    public void A_pending_earning_says_when_it_clears()
    {
        // The platform made the clearing rule, so the platform states the date. A client adding
        // days to a date it half-knows the rule for is a promise nobody checked.
        var earning = Record(share: 100, gross: 100).Value;

        var response = CreatorEarningMapper.ToResponse(earning, clearingDays: 30);

        response.AvailableAt.Should().Be(Now.AddDays(30));
    }

    [Fact]
    public void A_cleared_earning_has_no_clearing_date_left_to_state()
    {
        var earning = Record(share: 100, gross: 100).Value;
        earning.MakeAvailable(Now);

        CreatorEarningMapper.ToResponse(earning, clearingDays: 30).AvailableAt.Should().BeNull();
    }

    [Fact]
    public void An_earning_describes_itself_in_words()
    {
        // Composed from the source and the subject rather than stored, so it cannot drift from the
        // fields beside it.
        var donation = Record(share: 100, gross: 100).Value;

        CreatorEarningMapper.ToResponse(donation).Description.Should().Be("A donation");
    }
}

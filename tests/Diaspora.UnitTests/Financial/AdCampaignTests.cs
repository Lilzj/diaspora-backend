using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The advertising campaign — MON-GAP-001, -002, -006 and -007.
/// <para>
/// <b>What the register said was missing was not a route; it was the entity.</b>
/// <c>MonetizationProductCategory.Campaign</c> existed as an enum member and nothing else, so a
/// client that bought one and drew a dashboard was drawing something the platform did not store.
/// These pin the twelve-state machine that fills it, and in particular the transitions that must
/// <em>not</em> happen — a paused campaign that keeps charging, a funded one that can still be
/// edited, a rejection nobody has to justify.
/// </para>
/// </summary>
public class AdCampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    // ── Drafting ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_campaign_starts_as_an_editable_draft_that_has_spent_nothing()
    {
        var campaign = Drafted();

        campaign.Status.Should().Be(AdCampaignStatus.Draft);
        campaign.IsEditable.Should().BeTrue();
        campaign.Spend.MinorUnits.Should().Be(0);
        campaign.RemainingBudget.Should().Be(campaign.Budget);
        campaign.ReviewDecision.Should().Be(AdReviewDecision.Pending);
    }

    [Fact]
    public void A_campaign_with_nothing_to_spend_is_refused()
    {
        // It would sit active for ever showing a zero, which reads as a broken campaign rather
        // than an empty one.
        AdCampaign.Draft(
            AdCampaignId.New(), Guid.NewGuid(), "No budget", AdCampaignObjective.Awareness,
            Money.FromMinorUnits(0, "GBP"), Window())
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignBudgetRequired);
    }

    [Fact]
    public void An_advertising_campaign_may_not_run_open_ended()
    {
        // A fundraising campaign may; this one may not. There would be no ceiling in time, and
        // nothing for the completion sweep to fire on.
        AdCampaign.Draft(
            AdCampaignId.New(), Guid.NewGuid(), "Forever", AdCampaignObjective.Awareness,
            Money.FromMinorUnits(10_000, "GBP"), DateWindow.Open(Now))
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignScheduleRequiresEnd);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void A_campaign_needs_a_name_somebody_can_read(string name)
    {
        AdCampaign.Draft(
            AdCampaignId.New(), Guid.NewGuid(), name, AdCampaignObjective.Traffic,
            Money.FromMinorUnits(10_000, "GBP"), Window())
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNameLength);
    }

    // ── Editing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_draft_can_be_rewritten()
    {
        var campaign = Drafted();

        campaign.Update(
            "Second thoughts", AdCampaignObjective.Leads,
            Money.FromMinorUnits(50_000, "GBP"), Window(),
            AdAudience.Create(countryCodes: ["gb", "NG"])).IsSuccess.Should().BeTrue();

        campaign.Name.Should().Be("Second thoughts");
        campaign.Objective.Should().Be(AdCampaignObjective.Leads);
        campaign.Audience.CountryCodes.Should().BeEquivalentTo(["GB", "NG"]);
    }

    [Fact]
    public void A_funded_campaign_can_no_longer_be_changed()
    {
        // The budget is a sum somebody paid and the audience is what a reviewer approved. Letting
        // either move would mean the thing that ran was not the thing that was bought.
        var campaign = Funded();

        campaign.Update(
            "Sneaky edit", AdCampaignObjective.Awareness,
            Money.FromMinorUnits(1, "GBP"), Window(), null)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNotEditable);

        campaign.SetCreative(Guid.NewGuid(), "New headline", null)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNotEditable);
    }

    // ── Submission ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Submitting_waits_for_payment_before_it_waits_for_review()
    {
        // One step would put a campaign in front of a reviewer with no money behind it.
        var campaign = Drafted();

        campaign.Submit(Now).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.PendingPayment);
        campaign.SubmittedAt.Should().Be(Now);
        campaign.IsEditable.Should().BeFalse("submission is what freezes it, not payment");
    }

    [Fact]
    public void A_campaign_whose_dates_have_passed_cannot_be_submitted()
    {
        var campaign = AdCampaign.Draft(
            AdCampaignId.New(), Guid.NewGuid(), "Too late", AdCampaignObjective.Awareness,
            Money.FromMinorUnits(10_000, "GBP"),
            DateWindow.Create(Now.AddDays(-30), Now.AddDays(-1))).Value;

        campaign.Submit(Now).Error.Code.Should().Be(FinancialErrorCodes.AdCampaignScheduleAlreadyOver);
    }

    [Fact]
    public void Funding_queues_it_for_review()
    {
        var campaign = Drafted();
        campaign.Submit(Now);

        campaign.MarkFunded(MonetizationPurchaseId.New()).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.Submitted);
        campaign.PurchaseId.Should().NotBeNull();
    }

    [Fact]
    public void A_payment_that_never_captured_fails_rather_than_being_rejected()
    {
        // Nobody decided this. Rendering it as a decision would tell an advertiser they were
        // refused when they were not.
        var campaign = Drafted();
        campaign.Submit(Now);

        campaign.Fail("The payment did not capture.", Now).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.Failed);
        campaign.ReviewDecision.Should().Be(
            AdReviewDecision.Pending, "no reviewer ever saw it");
    }

    // ── Review ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Approval_records_who_and_when_and_waits_for_the_start_date()
    {
        var campaign = Funded();
        var reviewer = Guid.NewGuid();

        campaign.BeginReview().IsSuccess.Should().BeTrue();
        campaign.Status.Should().Be(AdCampaignStatus.UnderReview);

        campaign.Approve(reviewer, "Looks fine.", Now).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(
            AdCampaignStatus.Approved, "approval is not the same as going live");
        campaign.ReviewDecision.Should().Be(AdReviewDecision.Approved);
        campaign.ReviewedByUserId.Should().Be(reviewer);
        campaign.ReviewedAt.Should().Be(Now);
    }

    [Fact]
    public void A_rejection_without_a_reason_is_refused()
    {
        // The advertiser can neither act on it nor meaningfully appeal it.
        var campaign = Funded();

        campaign.Reject(Guid.NewGuid(), "   ", Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignRejectionNeedsReason);

        campaign.Status.Should().Be(AdCampaignStatus.Submitted, "nothing moved");
    }

    [Fact]
    public void A_rejection_carries_its_reason_to_the_advertiser()
    {
        var campaign = Funded();

        campaign.Reject(Guid.NewGuid(), "The creative names a competitor.", Now)
            .IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.Rejected);
        campaign.ReviewNote.Should().Be("The creative names a competitor.");
        campaign.EndedAt.Should().Be(Now);
    }

    [Fact]
    public void Only_a_rejected_campaign_can_be_appealed_and_only_once()
    {
        var campaign = Funded();

        campaign.Appeal("Please look again.", Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNothingToAppeal);

        campaign.Reject(Guid.NewGuid(), "No.", Now);

        campaign.Appeal("The competitor reference is our own brand.", Now)
            .IsSuccess.Should().BeTrue();
        campaign.AppealNote.Should().Be("The competitor reference is our own brand.");

        campaign.Appeal("Again.", Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignAlreadyAppealed);
    }

    [Fact]
    public void An_appeal_does_not_restart_the_campaign()
    {
        // It is a request for a second look. Showing it as running would show a campaign that is
        // not running.
        var campaign = Funded();
        campaign.Reject(Guid.NewGuid(), "No.", Now);

        campaign.Appeal("Please reconsider.", Now);

        campaign.Status.Should().Be(AdCampaignStatus.Rejected);
    }

    // ── Delivery ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_campaign_cannot_start_before_its_start_date()
    {
        var campaign = Approved(startsAt: Now.AddDays(7));

        campaign.Activate(Now).Error.Code.Should().Be(FinancialErrorCodes.AdCampaignScheduleNotStarted);
    }

    [Fact]
    public void Activation_makes_it_deliver()
    {
        var campaign = Active();

        campaign.Status.Should().Be(AdCampaignStatus.Active);
        campaign.IsDelivering.Should().BeTrue();
        campaign.ActivatedAt.Should().Be(Now);
    }

    [Fact]
    public void Spend_moves_only_while_the_campaign_is_running()
    {
        // The one that matters most. A paused campaign that kept charging would leave the
        // advertiser with a number going up and no way to see why.
        var campaign = Active();
        campaign.Pause(Now);

        campaign.RecordSpend(Money.FromMinorUnits(100, "GBP"), Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNotDelivering);

        campaign.Spend.MinorUnits.Should().Be(0);
    }

    [Fact]
    public void A_draft_cannot_be_charged_against()
    {
        Drafted().RecordSpend(Money.FromMinorUnits(100, "GBP"), Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignNotDelivering);
    }

    [Fact]
    public void Spend_accumulates_and_reduces_what_is_left()
    {
        var campaign = Active();

        campaign.RecordSpend(Money.FromMinorUnits(2_500, "GBP"), Now).IsSuccess.Should().BeTrue();
        campaign.RecordSpend(Money.FromMinorUnits(1_500, "GBP"), Now).IsSuccess.Should().BeTrue();

        campaign.Spend.MinorUnits.Should().Be(4_000);
        campaign.RemainingBudget.MinorUnits.Should().Be(6_000);
    }

    [Fact]
    public void Spend_is_clamped_to_the_budget_and_completes_the_campaign()
    {
        // Refusing would lose the delivery that already happened; charging past the ceiling would
        // charge money nobody authorised.
        var campaign = Active();

        campaign.RecordSpend(Money.FromMinorUnits(999_999, "GBP"), Now).IsSuccess.Should().BeTrue();

        campaign.Spend.Should().Be(campaign.Budget);
        campaign.RemainingBudget.MinorUnits.Should().Be(0);
        campaign.Status.Should().Be(AdCampaignStatus.Completed);
    }

    [Fact]
    public void Spend_in_another_currency_is_refused()
    {
        Active().RecordSpend(Money.FromMinorUnits(100, "NGN"), Now)
            .Error.Code.Should().Be(FinancialErrorCodes.AdCampaignCurrencyMismatch);
    }

    [Fact]
    public void Pausing_and_resuming_returns_it_to_delivering()
    {
        var campaign = Active();

        campaign.Pause(Now).IsSuccess.Should().BeTrue();
        campaign.Status.Should().Be(AdCampaignStatus.Paused);
        campaign.PausedAt.Should().Be(Now);

        campaign.Resume(Now.AddHours(2)).IsSuccess.Should().BeTrue();
        campaign.Status.Should().Be(AdCampaignStatus.Active);
        campaign.PausedAt.Should().BeNull();
    }

    [Fact]
    public void A_campaign_whose_schedule_ran_out_while_paused_completes_rather_than_resuming()
    {
        // Resuming into a window that has closed would deliver nothing while looking like it was
        // delivering.
        var campaign = Active();
        campaign.Pause(Now);

        campaign.Resume(Now.AddDays(90)).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.Completed);
        campaign.EndReason.Should().Contain("paused");
    }

    // ── Endings ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancelling_records_what_is_owed_back()
    {
        var campaign = Active();
        campaign.RecordSpend(Money.FromMinorUnits(3_000, "GBP"), Now);

        campaign.Cancel("Changed our minds.", Now).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(AdCampaignStatus.Cancelled);
        campaign.SettledAmount!.MinorUnits.Should().Be(7_000);
    }

    [Fact]
    public void A_completed_campaign_owes_nothing_back()
    {
        // It spent what it was going to spend. Cancelled and Completed are both endings and the
        // difference between them is exactly this figure.
        var campaign = Active();

        campaign.Complete("The schedule ended.", Now).IsSuccess.Should().BeTrue();

        campaign.SettledAmount.Should().BeNull();
    }

    [Theory]
    [InlineData(AdCampaignStatus.Completed)]
    [InlineData(AdCampaignStatus.Cancelled)]
    public void A_finished_campaign_cannot_finish_again(AdCampaignStatus ending)
    {
        var campaign = Active();

        if (ending == AdCampaignStatus.Completed)
        {
            campaign.Complete("Done.", Now);
        }
        else
        {
            campaign.Cancel("Stopped.", Now);
        }

        campaign.Cancel("Again.", Now).Error.Code
            .Should().Be(FinancialErrorCodes.AdCampaignAlreadyFinished);
    }

    // ── Audience ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_audience_that_names_nothing_is_everyone()
    {
        var audience = AdAudience.Everyone();

        audience.IsEveryone.Should().BeTrue();
        audience.ConstrainedDimensions().Should().BeEmpty();
    }

    [Fact]
    public void An_audience_normalises_what_it_is_given()
    {
        // An advertiser pasting a list with a trailing blank line should get their campaign, not
        // a validation error — and the stored value is what the estimate and the delivery engine
        // both read, so it has to be the normalised one.
        var audience = AdAudience.Create(
            countryCodes: ["gb", "GB", " ng ", "", "toolong"],
            interests: ["Tech", "tech", "  "]);

        audience.CountryCodes.Should().BeEquivalentTo(["GB", "NG"]);
        audience.Interests.Should().ContainSingle().Which.Should().Be("Tech");
    }

    [Fact]
    public void An_audience_reports_which_dimensions_it_narrows()
    {
        var audience = AdAudience.Create(
            countryCodes: ["GB"], communityIds: [Guid.NewGuid()]);

        audience.ConstrainedDimensions().Should().BeEquivalentTo(
            [AdTargetingDimension.Country, AdTargetingDimension.Community]);
    }

    [Fact]
    public void A_dimension_is_capped_rather_than_refused()
    {
        var audience = AdAudience.Create(
            countryCodes: Enumerable.Range(0, 200).Select(index => $"C{index % 26}{index / 26}"));

        audience.CountryCodes.Count.Should().BeLessThanOrEqualTo(AdAudience.MaximumPerDimension);
    }

    [Fact]
    public void Two_audiences_built_from_the_same_selection_are_equal_whatever_the_order()
    {
        var first = AdAudience.Create(countryCodes: ["GB", "NG"], interests: ["a", "b"]);
        var second = AdAudience.Create(countryCodes: ["NG", "GB"], interests: ["b", "a"]);

        first.Should().Be(second);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static DateWindow Window(DateTimeOffset? startsAt = null) =>
        DateWindow.Create(startsAt ?? Now, (startsAt ?? Now).AddDays(30));

    private static AdCampaign Drafted() =>
        AdCampaign.Draft(
            AdCampaignId.New(),
            Guid.NewGuid(),
            "Nigerians in London — spring",
            AdCampaignObjective.Traffic,
            Money.FromMinorUnits(10_000, "GBP"),
            Window()).Value;

    private static AdCampaign Funded()
    {
        var campaign = Drafted();
        campaign.Submit(Now);
        campaign.MarkFunded(MonetizationPurchaseId.New());
        return campaign;
    }

    private static AdCampaign Approved(DateTimeOffset? startsAt = null)
    {
        var campaign = AdCampaign.Draft(
            AdCampaignId.New(), Guid.NewGuid(), "Scheduled ahead", AdCampaignObjective.Awareness,
            Money.FromMinorUnits(10_000, "GBP"), Window(startsAt)).Value;

        campaign.Submit(Now);
        campaign.MarkFunded(MonetizationPurchaseId.New());
        campaign.Approve(Guid.NewGuid(), null, Now);
        return campaign;
    }

    private static AdCampaign Active()
    {
        var campaign = Approved();
        campaign.MarkScheduled();
        campaign.Activate(Now);
        return campaign;
    }
}

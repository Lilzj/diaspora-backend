using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Memberships;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// B15.1's domain: the campaign state machine, the membership binding and the recurring schedule.
/// <para>
/// <b>The state machine is asserted exhaustively rather than by example.</b> Twelve states is a
/// table, and a table tested by three happy paths is a table with nine untested rows — which is how
/// a campaign ends up able to go from <c>Cancelled</c> back to <c>Active</c> and nobody notices
/// until somebody donates to a campaign that was called off.
/// </para>
/// </summary>
public sealed class GivingExtensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-006 — the campaign state machine
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Every legal move in the register's table, one theory case each.
    /// <para>
    /// Transcribed from the frontend's <c>CAMPAIGN_TRANSITIONS</c>, which is the client's copy of a
    /// decision the server owns. If the two ever disagree, this is where it shows.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(CampaignStatus.Draft, CampaignStatus.PendingReview)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.PendingReview, CampaignStatus.Published)]
    [InlineData(CampaignStatus.PendingReview, CampaignStatus.Rejected)]
    [InlineData(CampaignStatus.PendingReview, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Rejected, CampaignStatus.Draft)]
    [InlineData(CampaignStatus.Rejected, CampaignStatus.PendingReview)]
    [InlineData(CampaignStatus.Rejected, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Published, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Published, CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Published, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Published, CampaignStatus.Closed)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Active, CampaignStatus.GoalReached)]
    [InlineData(CampaignStatus.Active, CampaignStatus.DeadlineReached)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Closed)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Refunding)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.Paused, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Paused, CampaignStatus.Closed)]
    [InlineData(CampaignStatus.Paused, CampaignStatus.Cancelled)]
    [InlineData(CampaignStatus.GoalReached, CampaignStatus.Active)]
    [InlineData(CampaignStatus.GoalReached, CampaignStatus.Completed)]
    [InlineData(CampaignStatus.GoalReached, CampaignStatus.Closed)]
    [InlineData(CampaignStatus.DeadlineReached, CampaignStatus.Completed)]
    [InlineData(CampaignStatus.DeadlineReached, CampaignStatus.Closed)]
    [InlineData(CampaignStatus.DeadlineReached, CampaignStatus.Refunding)]
    [InlineData(CampaignStatus.Refunding, CampaignStatus.Closed)]
    public void Every_transition_the_register_permits_is_legal(
        CampaignStatus from, CampaignStatus to) =>
        CampaignTransitions.CanTransition(from, to).Should().BeTrue();

    /// <summary>
    /// The moves that matter and are refused.
    /// <para>
    /// <b>The three terminal states lead nowhere, and the reopening cases are the dangerous ones.</b>
    /// A cancelled campaign that could become active again is a campaign whose refunds may already
    /// have gone out; a completed one that could collect again is a promise broken.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(CampaignStatus.Completed, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Completed, CampaignStatus.Published)]
    [InlineData(CampaignStatus.Cancelled, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Cancelled, CampaignStatus.Draft)]
    [InlineData(CampaignStatus.Closed, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Closed, CampaignStatus.Published)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Published)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Completed)]
    [InlineData(CampaignStatus.PendingReview, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Rejected, CampaignStatus.Published)]
    [InlineData(CampaignStatus.Paused, CampaignStatus.GoalReached)]
    [InlineData(CampaignStatus.Refunding, CampaignStatus.Active)]
    public void Every_transition_the_register_forbids_is_refused(
        CampaignStatus from, CampaignStatus to) =>
        CampaignTransitions.CanTransition(from, to).Should().BeFalse();

    /// <summary>
    /// The three states that collect, and only those.
    /// <para>
    /// <c>GoalReached</c> is on the list because people give past a target on purpose;
    /// <c>DeadlineReached</c> is not, because a deadline is a promise about when.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(CampaignStatus.Published, true)]
    [InlineData(CampaignStatus.Active, true)]
    [InlineData(CampaignStatus.GoalReached, true)]
    [InlineData(CampaignStatus.Draft, false)]
    [InlineData(CampaignStatus.PendingReview, false)]
    [InlineData(CampaignStatus.Rejected, false)]
    [InlineData(CampaignStatus.Paused, false)]
    [InlineData(CampaignStatus.DeadlineReached, false)]
    [InlineData(CampaignStatus.Completed, false)]
    [InlineData(CampaignStatus.Refunding, false)]
    [InlineData(CampaignStatus.Cancelled, false)]
    [InlineData(CampaignStatus.Closed, false)]
    [InlineData(CampaignStatus.Suspended, false)]
    public void Only_three_states_accept_donations(CampaignStatus status, bool accepts) =>
        CampaignTransitions.AcceptsDonations(status).Should().Be(accepts);

    /// <summary>Every one of the twelve is in the table, so none silently leads nowhere.</summary>
    [Fact]
    public void Every_campaign_state_is_in_the_transition_table()
    {
        foreach (var status in Enum.GetValues<CampaignStatus>())
        {
            var moves = CampaignTransitions.From(status);

            if (CampaignTransitions.IsTerminal(status))
            {
                moves.Should().BeEmpty($"{status} is terminal");
            }
            else
            {
                moves.Should().NotBeEmpty($"{status} must lead somewhere");
            }
        }
    }

    /// <summary>A campaign is refused outright without terms acceptance. Not a form-level check.</summary>
    [Fact]
    public void A_campaign_cannot_be_created_without_accepting_the_terms()
    {
        var created = Open(acceptedTerms: false);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().Be(FinancialErrorCodes.CampaignTermsNotAccepted);
    }

    /// <summary>Terms acceptance is stored as the moment it happened, not as a boolean.</summary>
    [Fact]
    public void Accepting_the_terms_records_when()
    {
        var campaign = Open().Value;

        campaign.AcceptedTermsAt.Should().Be(Now);
    }

    /// <summary>
    /// The load-bearing rule: no cause, no collecting.
    /// <para>
    /// A draft has no <c>causeCode</c>, so <c>canDonate</c> is false whatever its state says — and
    /// the client draws no donate button for one.
    /// </para>
    /// </summary>
    [Fact]
    public void A_campaign_with_no_cause_never_accepts_donations()
    {
        var campaign = Open().Value;

        campaign.CauseCode.Should().BeNull();
        campaign.AcceptsDonations.Should().BeFalse();
    }

    /// <summary>Publishing links the cause and sets the code, together.</summary>
    [Fact]
    public void Publishing_links_the_cause_and_opens_collection()
    {
        var campaign = Open().Value;

        campaign.MoveTo(CampaignStatus.PendingReview, Now).IsSuccess.Should().BeTrue();

        var cause = Cause();

        campaign.Publish(cause, Now).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(CampaignStatus.Published);
        campaign.CauseCode.Should().Be(cause.Code);
        campaign.CauseId.Should().Be(cause.Id);
        campaign.AcceptsDonations.Should().BeTrue();
        campaign.PublishedAt.Should().Be(Now);
    }

    /// <summary>
    /// <b>Publication cannot be reached through the ordinary move.</b> Routing it through
    /// <c>MoveTo</c> would make the cause a parameter somebody could pass null for, and a published
    /// campaign with no cause is the one thing the register forbids.
    /// </summary>
    [Fact]
    public void Publishing_through_the_ordinary_transition_is_refused()
    {
        var campaign = Open().Value;

        campaign.MoveTo(CampaignStatus.PendingReview, Now);

        var moved = campaign.MoveTo(CampaignStatus.Published, Now);

        moved.IsFailure.Should().BeTrue();
        moved.Error.Code.Should().Be(FinancialErrorCodes.CampaignTransitionInvalid);
        campaign.CauseCode.Should().BeNull();
    }

    /// <summary>A cause in another currency cannot back a campaign. The platform never converts.</summary>
    [Fact]
    public void A_campaign_cannot_publish_against_a_cause_in_another_currency()
    {
        var campaign = Open().Value;

        campaign.MoveTo(CampaignStatus.PendingReview, Now);

        var published = campaign.Publish(Cause(currency: "USD"), Now);

        published.IsFailure.Should().BeTrue();
        published.Error.Code.Should().Be(FinancialErrorCodes.CurrencyMismatch);
        campaign.Status.Should().Be(CampaignStatus.PendingReview);
    }

    /// <summary>A move to the state it already holds succeeds and changes nothing. The sweep's guarantee.</summary>
    [Fact]
    public void Moving_to_the_state_it_already_holds_is_a_no_op()
    {
        var campaign = Open().Value;
        var revision = campaign.Revision;

        campaign.MoveTo(CampaignStatus.Draft, Now).IsSuccess.Should().BeTrue();

        campaign.Revision.Should().Be(revision);
        campaign.DomainEvents.Should().BeEmpty();
    }

    /// <summary>An illegal move is refused with the conflict code the route answers 409 from.</summary>
    [Fact]
    public void An_illegal_transition_is_a_conflict()
    {
        var campaign = Open().Value;

        var moved = campaign.MoveTo(CampaignStatus.Completed, Now);

        moved.IsFailure.Should().BeTrue();
        moved.Error.Code.Should().Be(FinancialErrorCodes.CampaignTransitionInvalid);
        moved.Error.Type.Should().Be(Diaspora.SharedKernel.Results.ErrorType.Conflict);
    }

    /// <summary>Nothing moves out of a terminal state, however it got there.</summary>
    [Fact]
    public void A_terminal_campaign_moves_nowhere()
    {
        var campaign = Open().Value;

        campaign.MoveTo(CampaignStatus.Cancelled, Now).IsSuccess.Should().BeTrue();

        foreach (var target in Enum.GetValues<CampaignStatus>()
            .Where(status => status != CampaignStatus.Cancelled))
        {
            campaign.MoveTo(target, Now).IsFailure.Should().BeTrue($"cancelled cannot become {target}");
        }
    }

    /// <summary>A campaign that has ended cannot have its story rewritten.</summary>
    [Fact]
    public void A_closed_campaign_cannot_be_edited()
    {
        var campaign = Open().Value;

        campaign.MoveTo(CampaignStatus.Cancelled, Now);

        var revised = campaign.Revise(
            "A new title", null, null, null, null, null, null, null, null, null, null, Now);

        revised.IsFailure.Should().BeTrue();
        revised.Error.Code.Should().Be(FinancialErrorCodes.CampaignNotEditable);
        campaign.Title.Should().Be("Help Ada travel home");
    }

    // ── Moderation (7 Nov 2026) ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A suspended campaign is hidden, takes no money, cannot be edited, and tells its organiser why.
    /// </summary>
    [Fact]
    public void A_suspended_campaign_is_hidden_frozen_and_carries_the_reason()
    {
        var campaign = PublishedCampaign();

        campaign.Suspend("Reported as a copy of another member's appeal.", Now).IsSuccess
            .Should().BeTrue();

        campaign.Status.Should().Be(CampaignStatus.Suspended);
        campaign.IsPubliclyVisible.Should().BeFalse();
        campaign.AcceptsDonations.Should().BeFalse();
        campaign.IsEditable.Should().BeFalse();
        campaign.ReviewNote.Should().Be("Reported as a copy of another member's appeal.");

        campaign.DomainEvents.OfType<CampaignStatusChangedDomainEvent>().Last().Reason
            .Should().Be("Reported as a copy of another member's appeal.");
    }

    [Fact]
    public void A_campaign_is_not_suspended_without_a_reason()
    {
        var campaign = PublishedCampaign();

        var suspended = campaign.Suspend(" ", Now);

        suspended.IsFailure.Should().BeTrue();
        suspended.Error.Code.Should().Be(FinancialErrorCodes.CampaignModerationInvalid);
        campaign.Status.Should().Be(CampaignStatus.Published);
    }

    /// <summary>Only a live campaign is suspended; a draft has nothing public to take down.</summary>
    [Fact]
    public void A_draft_cannot_be_suspended()
    {
        var campaign = Open().Value;

        campaign.Suspend("Anything", Now).Error.Code
            .Should().Be(FinancialErrorCodes.CampaignTransitionInvalid);
    }

    /// <summary>
    /// Restored, not resumed: the organiser decides whether it collects again, and the moderator's
    /// reason does not follow the campaign back into public view.
    /// </summary>
    [Fact]
    public void A_restored_campaign_comes_back_paused_without_the_moderators_note()
    {
        var campaign = PublishedCampaign();
        campaign.Suspend("Reported as fraudulent.", Now);

        campaign.Restore("The organiser sent the hospital's letter.", Now).IsSuccess
            .Should().BeTrue();

        campaign.Status.Should().Be(CampaignStatus.Paused);
        campaign.AcceptsDonations.Should().BeFalse();
        campaign.IsPubliclyVisible.Should().BeTrue();
        campaign.ReviewNote.Should().BeNull();
    }

    [Fact]
    public void A_campaign_restored_after_its_deadline_comes_back_past_it()
    {
        var campaign = PublishedCampaign(deadline: Now.AddDays(1));
        campaign.Suspend("Reported as fraudulent.", Now);

        campaign.Restore("Cleared.", Now.AddDays(2)).IsSuccess.Should().BeTrue();

        campaign.Status.Should().Be(CampaignStatus.DeadlineReached);
    }

    /// <summary>The moves in and out of a suspension are moderation's, and named as such.</summary>
    [Fact]
    public void Suspending_and_restoring_are_moderation_moves()
    {
        CampaignTransitions.IsModerationMove(CampaignStatus.Active, CampaignStatus.Suspended)
            .Should().BeTrue();
        CampaignTransitions.IsModerationMove(CampaignStatus.Suspended, CampaignStatus.Paused)
            .Should().BeTrue();
        CampaignTransitions.IsModerationMove(CampaignStatus.Active, CampaignStatus.Paused)
            .Should().BeFalse();
    }

    private static Campaign PublishedCampaign(DateTimeOffset? deadline = null)
    {
        var campaign = Campaign.Open(
            CampaignId.New(),
            Guid.NewGuid(),
            "Ada Nwosu",
            "Help Ada travel home",
            "A short summary of why.",
            "The whole story, at length.",
            CampaignCategory.Repatriation,
            BeneficiaryType.Member,
            "Ada Nwosu",
            "GBP",
            2,
            true,
            Now,
            target: Money.FromAmount(100m, "GBP"),
            deadline: deadline).Value;

        campaign.MoveTo(CampaignStatus.PendingReview, Now);
        campaign.Publish(Cause(), Now);

        return campaign;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-007 — milestones and fund usage
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A milestone is unreached until the cause's own total passes it.</summary>
    [Fact]
    public void A_milestone_is_reached_only_when_the_total_passes_it()
    {
        var campaign = Open().Value;

        campaign.SetMilestones([(Guid.NewGuid(), "Halfway", 5_000)], Now).IsSuccess.Should().BeTrue();

        campaign.Milestones.Single().IsReached.Should().BeFalse();

        campaign.EvaluateMilestones(4_999, Now).Should().BeEmpty();
        campaign.Milestones.Single().IsReached.Should().BeFalse();

        campaign.EvaluateMilestones(5_000, Now).Should().HaveCount(1);
        campaign.Milestones.Single().IsReached.Should().BeTrue();
        campaign.Milestones.Single().ReachedAt.Should().Be(Now);
    }

    /// <summary>
    /// Reached once, announced once.
    /// <para>
    /// A second evaluation at a higher total must not raise a second milestone event, or every
    /// donation after the halfway point would tell the supporters halfway had just been reached.
    /// </para>
    /// </summary>
    [Fact]
    public void A_reached_milestone_is_announced_exactly_once()
    {
        var campaign = Open().Value;

        campaign.SetMilestones([(Guid.NewGuid(), "Halfway", 5_000)], Now);
        campaign.ClearDomainEvents();

        campaign.EvaluateMilestones(6_000, Now).Should().HaveCount(1);
        campaign.DomainEvents.OfType<CampaignMilestoneReachedDomainEvent>().Should().HaveCount(1);

        campaign.ClearDomainEvents();

        campaign.EvaluateMilestones(9_000, Now).Should().BeEmpty();
        campaign.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Renaming a milestone at the same figure does not un-reach it.
    /// <para>
    /// A supporter has already been told it was reached; taking that back because somebody fixed a
    /// spelling would be the platform contradicting a notification it sent.
    /// </para>
    /// </summary>
    [Fact]
    public void Re_declaring_a_reached_milestone_keeps_it_reached()
    {
        var campaign = Open().Value;
        var id = Guid.NewGuid();

        campaign.SetMilestones([(id, "Halfway", 5_000)], Now);
        campaign.EvaluateMilestones(5_000, Now);

        campaign.SetMilestones([(id, "Half way there", 5_000)], Now).IsSuccess.Should().BeTrue();

        campaign.Milestones.Single().Label.Should().Be("Half way there");
        campaign.Milestones.Single().IsReached.Should().BeTrue();
    }

    /// <summary>Two milestones at one figure would be two answers to one question.</summary>
    [Fact]
    public void Two_milestones_cannot_sit_at_the_same_figure()
    {
        var campaign = Open().Value;

        var set = campaign.SetMilestones(
            [(Guid.NewGuid(), "Halfway", 5_000), (Guid.NewGuid(), "Also halfway", 5_000)], Now);

        set.IsFailure.Should().BeTrue();
        set.Error.Code.Should().Be(FinancialErrorCodes.CampaignMilestoneInvalid);
    }

    /// <summary>A spend the platform cannot prove is not marked as one it can.</summary>
    [Fact]
    public void A_fund_usage_line_is_verified_only_when_a_payout_backs_it()
    {
        var campaign = Open().Value;

        campaign.SetFundUsage(
            [
                (Guid.NewGuid(), "Flights", 40_000, 38_000, "Booked", null),
                (Guid.NewGuid(), "Documents", 10_000, 9_000, null, Guid.NewGuid())
            ],
            Now).IsSuccess.Should().BeTrue();

        campaign.AllocatedMinorUnits.Should().Be(50_000);
        campaign.DisbursedMinorUnits.Should().Be(47_000);
        campaign.FundUsage.Count(line => line.IsPlatformVerified).Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-003 — the membership payment binding
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Recording a binding announces the settlement, from the aggregate rather than a handler.</summary>
    [Fact]
    public void A_binding_announces_the_settlement_it_records()
    {
        var membershipId = Guid.NewGuid();

        var binding = MembershipPaymentBinding.Record(
            MembershipPaymentBindingId.New(),
            membershipId,
            PaymentIntentId.New(),
            Guid.NewGuid(),
            Money.FromAmount(25m, "GBP"),
            Now,
            Now);

        var announced = binding.DomainEvents
            .OfType<MembershipPaymentSettledDomainEvent>()
            .Single();

        announced.MembershipId.Should().Be(membershipId);
        announced.AmountMinorUnits.Should().Be(2_500);
        announced.IsReplay.Should().BeFalse();
    }

    /// <summary>The reconciliation replay is the same fact, marked as a replay.</summary>
    [Fact]
    public void Re_announcing_a_binding_marks_it_as_a_replay()
    {
        var binding = MembershipPaymentBinding.Record(
            MembershipPaymentBindingId.New(),
            Guid.NewGuid(),
            PaymentIntentId.New(),
            Guid.NewGuid(),
            Money.FromAmount(25m, "GBP"),
            Now,
            Now);

        binding.ClearDomainEvents();
        binding.Reannounce(Now.AddHours(1));

        var replayed = binding.DomainEvents
            .OfType<MembershipPaymentSettledDomainEvent>()
            .Single();

        replayed.IsReplay.Should().BeTrue();
        binding.ReannounceCount.Should().Be(1);
    }

    /// <summary>The subject a membership payment must name, built one way only.</summary>
    [Fact]
    public void The_membership_subject_is_the_community_membership_type()
    {
        var membershipId = Guid.NewGuid();
        var subject = MembershipPaymentBinding.SubjectFor(membershipId);

        subject.SubjectType.Should().Be(MonetizationSubjectType.CommunityMembership);
        subject.Matches(MonetizationSubjectType.CommunityMembership, membershipId).Should().BeTrue();
        subject.Matches(MonetizationSubjectType.Organization, membershipId).Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-008 — the recurring schedule
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The first charge is the one the member just made; the schedule starts an interval out.</summary>
    [Fact]
    public void A_schedule_starts_one_interval_after_the_first_gift()
    {
        var schedule = Schedule(intervalMonths: 3);

        schedule.NextChargeAt.Should().Be(Now.AddMonths(3));
        schedule.Status.Should().Be(RecurringDonationStatus.Active);
        schedule.ChargeCount.Should().Be(0);
    }

    /// <summary>An anonymous donation cannot recur: there would be nobody to charge.</summary>
    [Fact]
    public void An_anonymous_donation_cannot_recur()
    {
        var scheduled = RecurringDonation.Schedule(
            RecurringDonationId.New(),
            Guid.Empty,
            Cause(),
            Money.FromAmount(10m, "GBP"),
            1,
            Now);

        scheduled.IsFailure.Should().BeTrue();
        scheduled.Error.Code.Should().Be(FinancialErrorCodes.RecurringDonationIntervalInvalid);
    }

    /// <summary>A schedule in another currency than its cause is refused. The platform never converts.</summary>
    [Fact]
    public void A_schedule_cannot_be_denominated_differently_from_its_cause()
    {
        var scheduled = RecurringDonation.Schedule(
            RecurringDonationId.New(),
            Guid.NewGuid(),
            Cause(currency: "GBP"),
            Money.FromAmount(10m, "USD"),
            1,
            Now);

        scheduled.IsFailure.Should().BeTrue();
        scheduled.Error.Code.Should().Be(FinancialErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// The claim moves the date before anything is charged.
    /// <para>
    /// <b>This is the property that makes a crash cost a cycle rather than a double charge.</b>
    /// </para>
    /// </summary>
    [Fact]
    public void Claiming_moves_the_next_charge_forward_before_any_money_is_asked_for()
    {
        var schedule = Schedule(intervalMonths: 1);
        var due = Now.AddMonths(1);

        schedule.IsDue(due).Should().BeTrue();
        schedule.TryClaim(due).Should().BeTrue();

        schedule.NextChargeAt.Should().Be(due.AddMonths(1));
        schedule.ChargeCount.Should().Be(0, "nothing has been charged yet");
    }

    /// <summary>A second worker arriving after the first claimed finds nothing due.</summary>
    [Fact]
    public void A_claimed_schedule_cannot_be_claimed_again_in_the_same_cycle()
    {
        var schedule = Schedule(intervalMonths: 1);
        var due = Now.AddMonths(1);

        schedule.TryClaim(due).Should().BeTrue();
        schedule.TryClaim(due).Should().BeFalse();
    }

    /// <summary>
    /// A sweep that ran late does not walk the anniversary forward.
    /// <para>
    /// The next date is computed from the scheduled instant, not from now — otherwise an outage on
    /// the first of the month would move a member's donation to the third for ever.
    /// </para>
    /// </summary>
    [Fact]
    public void A_late_sweep_keeps_the_original_anniversary()
    {
        var schedule = Schedule(intervalMonths: 1);
        var due = Now.AddMonths(1);

        schedule.TryClaim(due.AddDays(2)).Should().BeTrue();

        schedule.NextChargeAt.Should().Be(due.AddMonths(1));
    }

    /// <summary>A successful charge clears the failure state and counts the occurrence.</summary>
    [Fact]
    public void A_charge_counts_the_occurrence_and_clears_the_failure()
    {
        var schedule = Schedule(intervalMonths: 1);

        schedule.TryClaim(Now.AddMonths(1));
        schedule.RecordFailure("Declined.", 3, Now.AddMonths(1));
        schedule.ConsecutiveFailures.Should().Be(1);

        schedule.TryClaim(schedule.NextChargeAt!.Value);
        schedule.RecordCharge(DonationId.New(), Now.AddMonths(2));

        schedule.ConsecutiveFailures.Should().Be(0);
        schedule.LastFailureReason.Should().BeNull();
        schedule.ChargeCount.Should().Be(1);
        schedule.Status.Should().Be(RecurringDonationStatus.Active);
    }

    /// <summary>
    /// Failures retry until the budget runs out, and then the schedule stops asking.
    /// <para>
    /// The alternative is a platform hammering somebody's bank every night for a card that was
    /// cancelled in March.
    /// </para>
    /// </summary>
    [Fact]
    public void A_schedule_suspends_itself_after_the_retry_budget_is_spent()
    {
        var schedule = Schedule(intervalMonths: 1);
        var moment = Now.AddMonths(1);

        for (var attempt = 1; attempt < RecurringDonation.MaxConsecutiveFailures; attempt++)
        {
            schedule.TryClaim(schedule.NextChargeAt!.Value).Should().BeTrue();
            schedule.RecordFailure("Declined.", 3, moment);

            schedule.Status.Should().Be(RecurringDonationStatus.Retrying);
            schedule.NextChargeAt.Should().NotBeNull();

            moment = schedule.NextChargeAt!.Value;
        }

        schedule.TryClaim(schedule.NextChargeAt!.Value).Should().BeTrue();
        schedule.RecordFailure("Declined.", 3, moment);

        schedule.Status.Should().Be(RecurringDonationStatus.Suspended);
        schedule.NextChargeAt.Should().BeNull();
        schedule.IsDue(moment.AddYears(1)).Should().BeFalse();
    }

    /// <summary>A retry never lands after the cycle it belongs to. Two charges in one period is the bug.</summary>
    [Fact]
    public void A_retry_never_overtakes_the_next_ordinary_cycle()
    {
        var schedule = Schedule(intervalMonths: 1);
        var due = Now.AddMonths(1);

        schedule.TryClaim(due);

        var nextCycle = schedule.NextChargeAt!.Value;

        schedule.RecordFailure("Declined.", retryDays: 90, due);

        schedule.NextChargeAt.Should().Be(nextCycle);
    }

    /// <summary>Cancelling twice is one instruction, and the second succeeds.</summary>
    [Fact]
    public void Cancelling_is_idempotent()
    {
        var schedule = Schedule(intervalMonths: 1);

        schedule.Cancel(Now).IsSuccess.Should().BeTrue();
        schedule.Cancel(Now).IsSuccess.Should().BeTrue();

        schedule.Status.Should().Be(RecurringDonationStatus.Cancelled);
        schedule.NextChargeAt.Should().BeNull();
        schedule.IsDue(Now.AddYears(1)).Should().BeFalse();
    }

    /// <summary>A closed cause ends the schedule as completed rather than cancelled by the donor.</summary>
    [Fact]
    public void A_closed_cause_completes_the_schedule()
    {
        var schedule = Schedule(intervalMonths: 1);

        schedule.CompleteBecauseCauseClosed(Now);

        schedule.Status.Should().Be(RecurringDonationStatus.Completed);
        schedule.NextChargeAt.Should().BeNull();
        schedule.CancelledAt.Should().BeNull("the donor did not ask for this");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Diaspora.SharedKernel.Results.Result<Campaign> Open(bool acceptedTerms = true) =>
        Campaign.Open(
            CampaignId.New(),
            Guid.NewGuid(),
            "Ada Nwosu",
            "Help Ada travel home",
            "A short summary of why.",
            "The whole story, at length.",
            CampaignCategory.Repatriation,
            BeneficiaryType.Member,
            "Ada Nwosu",
            "GBP",
            2,
            acceptedTerms,
            Now,
            target: Money.FromAmount(100m, "GBP"));

    private static DonationCause Cause(string currency = "GBP") =>
        DonationCause.Open(
            DonationCauseId.New(),
            $"cause-{Guid.NewGuid():n}",
            "Help Ada travel home",
            "A short summary of why.",
            BeneficiaryType.Member,
            "Ada Nwosu",
            currency,
            Now);

    private static RecurringDonation Schedule(int intervalMonths) =>
        RecurringDonation.Schedule(
            RecurringDonationId.New(),
            Guid.NewGuid(),
            Cause(),
            Money.FromAmount(10m, "GBP"),
            intervalMonths,
            Now).Value;
}

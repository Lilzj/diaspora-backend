using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The owner's rules for a moderation case (7 Nov 2026), on the aggregate itself.
/// <para>
/// The traps these replace: a case could be decided again and again; one moderator could take
/// anything down for good; nobody checked anybody; reports never reached the case at all.
/// </para>
/// </summary>
public sealed class ModerationCaseRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Owner = UserId.From(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly UserId First = UserId.From(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly UserId Second = UserId.From(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    private static ModerationItem Case() =>
        ModerationItem.OpenForReports(
            ModerationContentTypes.Job, ModerationAreas.Jobs, Guid.NewGuid(), Owner, "Care Assistant", Now);

    private static void Report(ModerationItem item, ModerationReasonCode code) =>
        item.AddReport(UserId.New(), code, code.ToString(), "detail", "jobs", Guid.NewGuid(), Now);

    [Fact]
    public void A_fraud_report_lists_the_case_and_a_single_ordinary_report_does_not()
    {
        var fraud = Case();
        Report(fraud, ModerationReasonCode.FraudOrScam);
        fraud.IsListed.Should().BeTrue();

        var ordinary = Case();
        Report(ordinary, ModerationReasonCode.Duplicate);
        ordinary.IsListed.Should().BeFalse("one report of anything but fraud is not enough");

        Report(ordinary, ModerationReasonCode.Duplicate);
        ordinary.IsListed.Should().BeTrue("a second report lists it");
    }

    [Fact]
    public void The_same_member_reporting_twice_counts_once()
    {
        var item = Case();
        var reporter = UserId.New();

        item.AddReport(reporter, ModerationReasonCode.Spam, "Spam", null, "jobs", Guid.NewGuid(), Now)
            .Should().BeTrue();
        item.AddReport(reporter, ModerationReasonCode.Spam, "Spam", null, "community", Guid.NewGuid(), Now)
            .Should().BeFalse();

        item.Reports.Should().HaveCount(1);
    }

    [Fact]
    public void The_cases_reason_is_the_most_serious_one_given()
    {
        var item = Case();
        Report(item, ModerationReasonCode.Duplicate);
        Report(item, ModerationReasonCode.AsksForFees);
        Report(item, ModerationReasonCode.Spam);

        item.ReasonCode.Should().Be(ModerationReasonCode.AsksForFees);
    }

    [Fact]
    public void A_case_is_decided_once()
    {
        var item = Case();
        Report(item, ModerationReasonCode.FraudOrScam);
        item.Assign(First, Now);
        item.Decide(First, ModerationOutcome.HidePendingReview, ModerationReasonCode.FraudOrScam,
            "Asks for bank details.", Now);

        var again = () => item.Decide(First, ModerationOutcome.NoAction, ModerationReasonCode.Other,
            "Thought about it again.", Now);

        again.Should().Throw<DomainException>().Which.Code.Should().Be("trust.case.already-decided");
    }

    [Fact]
    public void A_removal_is_a_proposal_until_somebody_else_confirms_it()
    {
        var item = Case();
        Report(item, ModerationReasonCode.FraudOrScam);
        item.Assign(First, Now);

        var (_, apply) = item.Decide(First, ModerationOutcome.Remove, ModerationReasonCode.FraudOrScam,
            "Fake company; registration number belongs to a bakery.", Now);

        apply.Should().Be(ModerationOutcome.HidePendingReview, "it comes down while the removal waits");
        item.CurrentOutcome.Should().Be(ModerationOutcome.HidePendingReview);
        item.SecondReview.Should().Be(SecondReviewKind.RemovalProposal);
        item.AppealDeadline.Should().BeNull();

        var self = () => item.CompleteSecondReview(First, true, null, "Confirming my own removal.", Now);
        self.Should().Throw<DomainException>().Which.Code.Should().Be("trust.case.same-reviewer");

        var (_, confirmed) = item.CompleteSecondReview(Second, true, null, "Checked the register. Agreed.", Now);

        confirmed.Should().Be(ModerationOutcome.Remove);
        item.CurrentOutcome.Should().Be(ModerationOutcome.Remove);
        item.AppealDeadline.Should().Be(Now.Add(ModerationPolicy.AppealWindow));
        item.DomainEvents.OfType<ModerationActionTakenDomainEvent>().Last().ConfirmedBy.Should().Be(Second);
    }

    [Fact]
    public void A_declined_removal_puts_the_reviewers_choice_in_its_place()
    {
        var item = Case();
        Report(item, ModerationReasonCode.FraudOrScam);
        item.Assign(First, Now);
        item.Decide(First, ModerationOutcome.Remove, ModerationReasonCode.FraudOrScam,
            "Looks like a fake company.", Now);

        var (_, apply) = item.CompleteSecondReview(Second, false, ModerationOutcome.Restore,
            "The company is real; the reporter confused two firms.", Now);

        apply.Should().Be(ModerationOutcome.Restore);
        item.CurrentOutcome.Should().Be(ModerationOutcome.Restore);
        item.Status.Should().Be(ModerationQueueStatus.Resolved);
    }

    [Fact]
    public void An_emergency_hide_waits_for_a_second_check_within_a_day()
    {
        var item = Case();
        Report(item, ModerationReasonCode.ChildSafety);
        item.Assign(First, Now);

        item.Decide(First, ModerationOutcome.HidePendingReview, ModerationReasonCode.ChildSafety,
            "Asks for photographs of the applicant's children.", Now);

        item.SecondReview.Should().Be(SecondReviewKind.EmergencyHide);
        item.SecondReviewDueAt.Should().Be(Now.Add(ModerationPolicy.EmergencySecondReview));
        item.IsOverdue(Now.AddHours(25)).Should().BeTrue();
    }

    [Fact]
    public void Ten_serious_reports_hide_it_before_anyone_has_looked_and_no_action_puts_it_back()
    {
        var item = Case();

        for (var count = 0; count < ModerationPolicy.AutomaticHideReports; count++)
        {
            Report(item, count == 0 ? ModerationReasonCode.ViolenceOrThreats : ModerationReasonCode.Spam);
        }

        item.ShouldHideAutomatically().Should().BeTrue();
        item.HideAutomatically(Now);
        item.HiddenAutomatically.Should().BeTrue();
        item.AwaitsDecision.Should().BeTrue("the system hid it; a person still has to decide");

        item.Assign(First, Now);
        var (_, apply) = item.Decide(First, ModerationOutcome.NoAction, ModerationReasonCode.Other,
            "A pile-on: the post is an ordinary complaint.", Now);

        apply.Should().Be(ModerationOutcome.Restore);
    }

    [Fact]
    public void Ten_reports_of_nothing_serious_hide_nothing()
    {
        var item = Case();

        for (var count = 0; count < ModerationPolicy.AutomaticHideReports; count++)
        {
            Report(item, ModerationReasonCode.Duplicate);
        }

        item.ShouldHideAutomatically().Should().BeFalse();
    }

    [Fact]
    public void Only_the_owner_can_ask_for_a_review_and_only_of_something_hidden()
    {
        var item = Case();
        Report(item, ModerationReasonCode.FraudOrScam);
        item.Assign(First, Now);
        item.Decide(First, ModerationOutcome.HidePendingReview, ModerationReasonCode.FraudOrScam,
            "Phone number is premium-rate.", Now);

        var stranger = () => item.RequestReview(Second, Now);
        stranger.Should().Throw<DomainException>().Which.Code.Should().Be("trust.case.not-owner");

        item.RequestReview(Owner, Now);

        item.AwaitsDecision.Should().BeTrue();
        item.AssignedModeratorId.Should().BeNull();
    }

    [Fact]
    public void A_note_shorter_than_ten_characters_is_refused()
    {
        var item = Case();
        Report(item, ModerationReasonCode.FraudOrScam);
        item.Assign(First, Now);

        var curt = () => item.Decide(First, ModerationOutcome.HidePendingReview, ModerationReasonCode.Spam, "spam", Now);

        curt.Should().Throw<DomainException>().Which.Code.Should().Be("trust.case.note-required");
    }

    [Fact]
    public void Staff_cannot_open_a_case_on_their_own_content()
    {
        var open = () => ModerationItem.OpenByStaff(
            ModerationContentTypes.Job, ModerationAreas.Jobs, Guid.NewGuid(), Owner, null, Owner,
            ModerationReasonCode.Spam, "My own advert, for some reason.", Now);

        open.Should().Throw<DomainException>().Which.Code.Should().Be("trust.review.self-forbidden");
    }
}

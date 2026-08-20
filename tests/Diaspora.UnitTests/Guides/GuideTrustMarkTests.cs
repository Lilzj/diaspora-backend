using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The guide's trust mark, which is a projection of Trust's decision and never a decision of
/// its own.
/// <para>
/// Nothing on the aggregate can grant a badge. Submission records that a claim went out;
/// every other transition arrives from a handler for a Trust integration event. That delivery
/// is <b>at-least-once and unordered</b>, which is the whole reason the guard exists: a
/// redelivery of last month's approval, arriving after this month's revocation, would
/// otherwise silently re-verify a guide Trust has already taken the badge from — and a
/// verified badge on wrong advice is exactly the failure the badge is supposed to prevent.
/// </para>
/// </summary>
public class GuideTrustMarkTests
{
    private static readonly DateTimeOffset Submitted = GuideLifecycleTests.Now;
    private static readonly DateTimeOffset Approved = Submitted.AddDays(1);
    private static readonly DateTimeOffset Revoked = Submitted.AddDays(30);

    private static readonly Guid CaseId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    // ── Submission ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_guide_carries_no_claim_at_all()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.TrustMark.Should().Be(GuideTrustMark.None);
        guide.TrustMark.State.Should().Be(GuideVerificationState.None);
        guide.TrustMark.ChangedAt.Should().BeNull();
    }

    [Fact]
    public void Only_a_published_guide_may_be_submitted_for_verification()
    {
        var guide = GuideLifecycleTests.Drafted();

        var submitted = guide.SubmitForVerification(Submitted, GuideLifecycleTests.Author);

        submitted.IsFailure.Should().BeTrue();
        submitted.Error.Should().Be(GuideErrors.NotPublished,
            "verifying a draft would badge content nobody has seen");
    }

    [Fact]
    public void Submitting_stamps_the_case_identifier_so_the_outcome_can_find_the_guide_again()
    {
        var guide = GuideLifecycleTests.Published();
        guide.ClearDomainEvents();

        var submitted = guide.SubmitForVerification(Submitted, GuideLifecycleTests.Author, CaseId);

        submitted.IsSuccess.Should().BeTrue();
        guide.TrustMark.State.Should().Be(GuideVerificationState.Pending);
        guide.TrustMark.VerificationId.Should().Be(CaseId,
            "Trust's events name a member, and a member has many guides — the case is the only " +
            "correlation an approval can land on");
        guide.TrustMark.ChangedAt.Should().Be(Submitted);

        var requested = guide.DomainEvents.OfType<GuideVerificationRequestedDomainEvent>().Single();
        requested.RequestedBy.Should().Be(GuideLifecycleTests.Author);
        requested.RequestedAt.Should().Be(Submitted);
    }

    [Fact]
    public void A_second_submission_while_one_is_under_review_is_refused()
    {
        var guide = GuideLifecycleTests.Published();

        guide.SubmitForVerification(Submitted, GuideLifecycleTests.Author, CaseId)
            .IsSuccess.Should().BeTrue();

        var again = guide.SubmitForVerification(
            Submitted.AddHours(1), GuideLifecycleTests.Author, Guid.NewGuid());

        again.IsFailure.Should().BeTrue();
        again.Error.Should().Be(GuideErrors.VerificationAlreadyPending);

        guide.TrustMark.VerificationId.Should().Be(CaseId,
            "the second attempt must not orphan the case the first one opened");
    }

    [Fact]
    public void A_guide_whose_claim_was_refused_may_be_submitted_again()
    {
        var guide = GuideLifecycleTests.Published();

        guide.SubmitForVerification(Submitted, GuideLifecycleTests.Author, CaseId)
            .IsSuccess.Should().BeTrue();

        guide.ApplyVerificationDecision(
            GuideVerificationState.None, CaseId, badge: null, "Sources not checkable.", Approved)
            .IsSuccess.Should().BeTrue();

        var resubmitted = guide.SubmitForVerification(
            Approved.AddDays(1), GuideLifecycleTests.Author, Guid.NewGuid());

        resubmitted.IsSuccess.Should().BeTrue(
            "an author who fixed the sources must have a way back");
    }

    // ── Applying an outcome ─────────────────────────────────────────────────────────────────

    [Fact]
    public void An_approval_grants_the_badge_and_announces_the_change()
    {
        var guide = PendingReview();
        guide.ClearDomainEvents();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, CaseId, "expert-reviewed", reason: null, Approved)
            .IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Verified);
        guide.TrustMark.Badge.Should().Be("expert-reviewed");
        guide.TrustMark.VerifiedAt.Should().Be(Approved);
        guide.TrustMark.ChangedAt.Should().Be(Approved);

        var changed = guide.DomainEvents.OfType<GuideVerificationChangedDomainEvent>().Single();
        changed.PreviousState.Should().Be(GuideVerificationState.Pending);
        changed.CurrentState.Should().Be(GuideVerificationState.Verified);
        changed.ChangedAt.Should().Be(Approved);
    }

    [Fact]
    public void A_revocation_takes_the_badge_back_and_keeps_the_reason_for_the_author()
    {
        var guide = VerifiedGuide();
        guide.ClearDomainEvents();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Revoked, CaseId, badge: null, "The visa rule changed.", Revoked)
            .IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Revoked);
        guide.TrustMark.Badge.Should().BeNull();
        guide.TrustMark.VerifiedAt.Should().BeNull();
        guide.TrustMark.Reason.Should().Be("The visa rule changed.");

        guide.DomainEvents.OfType<GuideVerificationChangedDomainEvent>().Single()
            .PreviousState.Should().Be(GuideVerificationState.Verified);
    }

    [Fact]
    public void A_redelivered_decision_that_changes_nothing_announces_nothing()
    {
        var guide = VerifiedGuide();
        guide.ClearDomainEvents();

        // The same approval, delivered a second time a moment later. At-least-once delivery
        // makes this the ordinary case, not the exotic one.
        guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, CaseId, "expert-reviewed", reason: null,
            Approved.AddTicks(1)).IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Verified);

        guide.DomainEvents.OfType<GuideVerificationChangedDomainEvent>().Should().BeEmpty(
            "a duplicate delivery must not notify the author twice that they were verified");
    }

    // ── The staleness guard, which is the point of this suite ───────────────────────────────

    [Fact]
    public void A_stale_approval_arriving_after_a_revocation_cannot_re_verify_the_guide()
    {
        var guide = VerifiedGuide();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Revoked, CaseId, badge: null, "The advice is out of date.",
            Revoked).IsSuccess.Should().BeTrue();

        guide.ClearDomainEvents();

        // The broker redelivers the original approval, weeks late and out of order.
        var replayed = guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, CaseId, "expert-reviewed", reason: null, Approved);

        replayed.IsSuccess.Should().BeTrue(
            "from the handler's point of view the event was processed correctly, so it must not " +
            "be retried forever");

        guide.TrustMark.State.Should().Be(GuideVerificationState.Revoked,
            "a verified badge on advice Trust has withdrawn is the exact failure the badge exists " +
            "to prevent");

        guide.TrustMark.Reason.Should().Be("The advice is out of date.");
        guide.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_decision_bearing_the_same_instant_as_the_current_mark_is_dropped()
    {
        var guide = VerifiedGuide();
        guide.ClearDomainEvents();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Revoked, CaseId, badge: null, "Duplicate.", Approved)
            .IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Verified,
            "equal timestamps cannot be ordered, so the stored decision wins and the redelivery loses");

        guide.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_newer_decision_always_wins_however_many_stale_ones_arrive_first()
    {
        var guide = VerifiedGuide();

        // A burst of redeliveries in every wrong order, then the real one.
        guide.ApplyVerificationDecision(
            GuideVerificationState.None, CaseId, null, "stale clear", Submitted);

        guide.ApplyVerificationDecision(
            GuideVerificationState.Pending, CaseId, null, null, Submitted.AddHours(1));

        guide.TrustMark.State.Should().Be(GuideVerificationState.Verified);

        guide.ApplyVerificationDecision(
            GuideVerificationState.Revoked, CaseId, null, "Genuinely revoked.", Revoked)
            .IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Revoked);
        guide.TrustMark.ChangedAt.Should().Be(Revoked);
    }

    [Fact]
    public void An_unclaimed_guide_accepts_any_decision_because_it_has_nothing_to_compare_against()
    {
        // A decision that arrives across a deployment gap, for a claim this instance never saw
        // submitted, still has to land.
        var guide = GuideLifecycleTests.Published();

        guide.TrustMark.ChangedAt.Should().BeNull();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, CaseId, "expert-reviewed", null, Approved)
            .IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.Verified);
    }

    [Fact]
    public void Clearing_a_mark_keeps_the_reason_and_drops_the_case()
    {
        var guide = VerifiedGuide();

        guide.ApplyVerificationDecision(
            GuideVerificationState.None, CaseId, badge: null, "The reviewer declined.",
            Approved.AddDays(1)).IsSuccess.Should().BeTrue();

        guide.TrustMark.State.Should().Be(GuideVerificationState.None);
        guide.TrustMark.VerificationId.Should().BeNull(
            "a cleared claim is closed; a later decision must not be attributed to it");
        guide.TrustMark.Reason.Should().Be("The reviewer declined.");
        guide.TrustMark.Badge.Should().BeNull();
    }

    [Fact]
    public void The_mark_reports_whether_a_decision_is_older_than_itself()
    {
        var mark = GuideTrustMark.Verified(CaseId, "expert-reviewed", Approved);

        mark.IsNewerThan(Approved.AddSeconds(-1)).Should().BeTrue();
        mark.IsNewerThan(Approved).Should().BeTrue("equal is not newer, so the redelivery loses");
        mark.IsNewerThan(Approved.AddSeconds(1)).Should().BeFalse();

        GuideTrustMark.None.IsNewerThan(Approved).Should().BeFalse(
            "an unclaimed mark has no decision to be newer than");
    }

    // ── Builders ────────────────────────────────────────────────────────────────────────────

    private static Guide PendingReview()
    {
        var guide = GuideLifecycleTests.Published();

        guide.SubmitForVerification(Submitted, GuideLifecycleTests.Author, CaseId)
            .IsSuccess.Should().BeTrue();

        return guide;
    }

    private static Guide VerifiedGuide()
    {
        var guide = PendingReview();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, CaseId, "expert-reviewed", reason: null, Approved)
            .IsSuccess.Should().BeTrue();

        return guide;
    }
}

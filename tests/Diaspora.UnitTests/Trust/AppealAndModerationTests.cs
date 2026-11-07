using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// Appeals, and the two fairness rules that make an appeals process worth having.
/// </summary>
public sealed class AppealTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId OriginalReviewer = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly UserId FreshReviewer = UserId.From(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private static Appeal Filed() => Appeal.File(
        Member, AppealType.Verification, "target-1",
        "The document was clear and my permit is valid until 2030.", Now);

    [Fact]
    public void A_filed_appeal_is_pending_and_already_carries_a_holding_explanation()
    {
        var appeal = Filed();

        appeal.Status.Should().Be(AppealStatus.Pending);
        appeal.IsOpen.Should().BeTrue();
        appeal.Explanation.Should().NotBeNullOrWhiteSpace(
            "the appeals screen renders this as reviewer comments and must never show a blank");
    }

    [Fact]
    public void A_member_cannot_review_their_own_appeal()
    {
        var appeal = Filed();

        var decide = () => appeal.Decide(Member, upheld: true, "I agree with myself.", Now);

        decide.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.review.self-forbidden");
    }

    /// <summary>
    /// The rule that distinguishes an appeal from a second opinion by the same person.
    /// </summary>
    [Fact]
    public void The_reviewer_whose_decision_is_appealed_cannot_decide_the_appeal()
    {
        var appeal = Filed().WithOriginalDecider(OriginalReviewer);

        var decide = () => appeal.Decide(
            OriginalReviewer, upheld: false, "I stand by my original decision.", Now);

        decide.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.review.self-forbidden");
    }

    [Fact]
    public void A_different_reviewer_may_decide_the_appeal()
    {
        var appeal = Filed().WithOriginalDecider(OriginalReviewer);

        appeal.Decide(FreshReviewer, upheld: true, "Permit checks out.", Now);

        appeal.Status.Should().Be(AppealStatus.Approved);
        appeal.ResolvedAt.Should().Be(Now);
        appeal.Reviews.Should().ContainSingle();
    }

    [Fact]
    public void An_appeal_filed_outside_the_window_is_refused()
    {
        var decidedLongAgo = Now - Appeal.AppealWindow - TimeSpan.FromDays(1);

        var file = () => Appeal.File(
            Member, AppealType.Verification, "target-1", "Please reconsider.", Now, decidedLongAgo);

        file.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.appeal.window-closed");
    }

    [Fact]
    public void An_appeal_filed_inside_the_window_is_accepted()
    {
        var decidedRecently = Now - TimeSpan.FromDays(10);

        var file = () => Appeal.File(
            Member, AppealType.Verification, "target-1", "Please reconsider.", Now, decidedRecently);

        file.Should().NotThrow();
    }

    [Fact]
    public void A_resolved_appeal_cannot_be_decided_again()
    {
        var appeal = Filed();
        appeal.Decide(FreshReviewer, upheld: false, "Declined.", Now);

        var again = () => appeal.Decide(FreshReviewer, upheld: true, "Actually, upheld.", Now);

        again.Should().Throw<DomainException>().WithMessage("*already resolved*");
    }

    [Fact]
    public void Escalation_keeps_the_appeal_open_and_records_the_step()
    {
        var appeal = Filed();

        appeal.Escalate(FreshReviewer, "Needs a second opinion.", Now);

        appeal.Status.Should().Be(AppealStatus.Escalated);
        appeal.IsOpen.Should().BeTrue();
        appeal.Reviews.Should().ContainSingle()
            .Which.ToStatus.Should().Be(AppealStatus.Escalated);
    }

    [Fact]
    public void Evidence_cannot_be_added_after_resolution()
    {
        var appeal = Filed();
        appeal.Decide(FreshReviewer, upheld: false, "Declined.", Now);

        var attach = () => appeal.AttachEvidence("storage/late-evidence");

        attach.Should().Throw<DomainException>();
    }

    [Fact]
    public void Evidence_is_capped()
    {
        var appeal = Filed();

        for (var index = 0; index < Appeal.MaxEvidenceItems; index++)
        {
            appeal.AttachEvidence($"storage/evidence-{index}");
        }

        var oneTooMany = () => appeal.AttachEvidence("storage/evidence-overflow");

        oneTooMany.Should().Throw<DomainException>();
        appeal.EvidenceKeys.Should().HaveCount(Appeal.MaxEvidenceItems);
    }
}

/// <summary>
/// The moderation queue: assignment, self-moderation, and the ordering signals.
/// </summary>
public sealed class ModerationItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Author = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Moderator = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static ModerationItem Queued() => ModerationItem.Enqueue(
        ModerationContentType.Guide, "guide-1", Author, Now, "Opening a UK bank account",
        sla: TimeSpan.FromHours(24));

    [Fact]
    public void A_moderator_cannot_take_their_own_content()
    {
        var item = ModerationItem.Enqueue(
            ModerationContentType.Guide, "guide-1", Moderator, Now);

        var assign = () => item.Assign(Moderator, Now);

        assign.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.review.self-forbidden");
    }

    /// <summary>
    /// Without this, two moderators can decide the same item differently in the same minute
    /// and the queue stops being a workflow.
    /// </summary>
    [Fact]
    public void An_unassigned_item_cannot_be_decided()
    {
        var item = Queued();

        var decide = () => item.Decide(
            Moderator, ModerationOutcome.NoAction, ModerationReasonCode.Other, "Looked at it: nothing wrong.", Now);

        decide.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.queue.not-assigned");
    }

    [Fact]
    public void An_item_assigned_to_someone_else_cannot_be_decided()
    {
        var item = Queued();
        item.Assign(Moderator, Now);

        var other = UserId.From(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var decide = () => item.Decide(
            other, ModerationOutcome.NoAction, ModerationReasonCode.Other, "Looked at it: nothing wrong.", Now);

        decide.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deciding_an_assigned_item_records_the_decision_and_resolves_it()
    {
        var item = Queued();
        item.Assign(Moderator, Now);

        var (decision, apply) = item.Decide(
            Moderator, ModerationOutcome.HidePendingReview, ModerationReasonCode.Spam,
            "Posted the same advert forty times.", Now);

        apply.Should().Be(ModerationOutcome.HidePendingReview);
        item.Status.Should().Be(ModerationQueueStatus.Rejected);
        item.ResolvedAt.Should().Be(Now);
        decision.PreviousStatus.Should().Be(ModerationQueueStatus.PendingReview);
        decision.NewStatus.Should().Be(ModerationQueueStatus.Rejected);
        decision.Reason.Should().Be("Posted the same advert forty times.");
    }

    [Fact]
    public void A_flagged_ai_review_raises_priority_but_decides_nothing()
    {
        var item = Queued();

        item.RecordAiReview(clean: false, 0.87m, ["spam"], Now);

        item.Priority.Should().Be(ModerationPriority.High);
        item.Status.Should().Be(ModerationQueueStatus.PendingReview,
            "the classifier orders the queue; only a moderator removes content");
    }

    [Fact]
    public void Report_volume_escalates_priority()
    {
        var item = Queued();

        item.RecordReport(Now);
        item.Priority.Should().Be(ModerationPriority.Normal, "one report is noise");

        item.RecordReport(Now);
        item.Priority.Should().Be(ModerationPriority.High);

        item.RecordReport(Now);
        item.RecordReport(Now);
        item.RecordReport(Now);
        item.Priority.Should().Be(ModerationPriority.Urgent);
    }

    [Fact]
    public void Ai_confidence_is_clamped_to_a_probability()
    {
        var item = Queued();

        item.RecordAiReview(clean: false, 4.2m, ["spam"], Now);

        item.AiConfidence.Should().Be(1m);
    }

    [Fact]
    public void A_decided_item_can_be_reopened_by_an_appeal()
    {
        var item = Queued();
        item.Assign(Moderator, Now);
        item.Decide(
            Moderator, ModerationOutcome.HidePendingReview, ModerationReasonCode.Spam,
            "Posted the same advert forty times.", Now);

        item.MarkAppealed(Now);

        item.Status.Should().Be(ModerationQueueStatus.Appealed);
        item.ResolvedAt.Should().BeNull();
        item.IsOpen.Should().BeTrue();
    }
}

/// <summary>Community confirmations — the one-per-member, never-your-own signal.</summary>
public sealed class CommunityContributionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Confirmer = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Author = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [Fact]
    public void A_member_cannot_confirm_their_own_contribution()
    {
        var record = () => CommunityContribution.Record(
            Author, Author, "community-update", "update-1", Now);

        record.Should().Throw<DomainException>()
            .Which.Code.Should().Be("trust.confirmation.self-forbidden");
    }

    [Fact]
    public void A_confirmation_records_who_earns_the_points()
    {
        var confirmation = CommunityContribution.Record(
            Confirmer, Author, "Community-Update", "update-1", Now, "gb");

        confirmation.AuthorUserId.Should().Be(Author);
        confirmation.ConfirmedByUserId.Should().Be(Confirmer);
        confirmation.TargetType.Should().Be("community-update", "target types are normalised");
        confirmation.CountryCode.Should().Be("GB");
    }
}

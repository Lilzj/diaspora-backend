using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The verification state machine.
/// <para>
/// These are the tests that matter most in the module. Every other rule can be worked around
/// by a determined caller reaching a different entry point; an illegal state transition
/// cannot, because the aggregate checks the table before every move. If these pass, "a
/// rejected verification cannot silently become approved" is a property of the code rather
/// than a convention.
/// </para>
/// </summary>
public sealed class VerificationWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Reviewer = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static VerificationRequest Queued(VerificationType type = VerificationType.Diaspora)
    {
        var request = VerificationRequest.Open(Member, type, Now);
        request.AttachDocument(VerificationDocumentType.Passport, "storage/key", Now);
        request.Queue(Now, TimeSpan.FromHours(48));
        return request;
    }

    [Fact]
    public void A_new_request_starts_submitted_and_holds_no_grant()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Diaspora, Now);

        request.Status.Should().Be(VerificationStatus.Submitted);
        request.IsGranted(Now).Should().BeFalse();
    }

    [Fact]
    public void Queueing_moves_to_pending_and_sets_the_review_deadline()
    {
        var request = Queued();

        request.Status.Should().Be(VerificationStatus.Pending);
        request.ReviewDueAt.Should().Be(Now.AddHours(48));
    }

    [Fact]
    public void Approval_grants_the_verification()
    {
        var request = Queued();

        request.Approve(Reviewer, "Compliance", null, Now);

        request.Status.Should().Be(VerificationStatus.Approved);
        request.IsGranted(Now).Should().BeTrue();
        request.ReviewedAt.Should().Be(Now);
    }

    [Fact]
    public void An_expired_grant_no_longer_counts()
    {
        var request = Queued();
        request.Approve(Reviewer, "Compliance", null, Now, expiresAt: Now.AddDays(1));

        request.IsGranted(Now).Should().BeTrue();
        request.IsGranted(Now.AddDays(2)).Should().BeFalse("the grant has lapsed");
    }

    /// <summary>
    /// The invariant the whole trust ladder rests on. If a member can approve their own
    /// verification, every capability gated on verification is self-service.
    /// </summary>
    [Fact]
    public void A_member_cannot_approve_their_own_verification()
    {
        var request = Queued();

        var approve = () => request.Approve(Member, "Self", null, Now);

        approve.Should().Throw<DomainException>()
            .WithMessage("*cannot review their own verification*");
    }

    [Fact]
    public void A_member_cannot_reject_their_own_verification()
    {
        var request = Queued();

        var reject = () => request.Reject(
            Member, "Self", VerificationRejectionReason.INVALID_DOCUMENT, "no", Now);

        reject.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_rejected_verification_cannot_jump_straight_to_approved()
    {
        var request = Queued();
        request.Reject(Reviewer, "Compliance", VerificationRejectionReason.INVALID_DOCUMENT, "Blurred.", Now);

        // Rejected → Approved is not in the transition table. The only route from a refusal
        // to a grant is through an appeal, or by reopening for a fresh review.
        var approve = () => request.Approve(Reviewer, "Compliance", null, Now);

        approve.Should().Throw<DomainException>()
            .WithMessage("*cannot move from 'Rejected' to 'Approved'*");
    }

    [Fact]
    public void A_rejected_verification_reaches_approved_through_an_upheld_appeal()
    {
        var request = Queued();
        request.Reject(Reviewer, "Compliance", VerificationRejectionReason.INVALID_DOCUMENT, "Blurred.", Now);

        request.MarkAppealed(Now);
        request.ResolveAppeal(upheld: true, "Clear on re-review.", Now);

        request.Status.Should().Be(VerificationStatus.Approved);
        request.RejectionReason.Should().BeNull("an upheld appeal clears the refusal");
    }

    [Fact]
    public void A_dismissed_appeal_restores_the_original_refusal()
    {
        var request = Queued();
        request.Reject(Reviewer, "Compliance", VerificationRejectionReason.MISMATCHED_NATIONALITY, "No.", Now);

        request.MarkAppealed(Now);
        request.ResolveAppeal(upheld: false, "Original decision stands.", Now);

        request.Status.Should().Be(VerificationStatus.Rejected);
    }

    [Fact]
    public void Only_appealable_outcomes_can_be_appealed()
    {
        var request = Queued();

        // Still pending — there is no decision to appeal yet.
        var appeal = () => request.MarkAppealed(Now);

        appeal.Should().Throw<DomainException>().WithMessage("*cannot be appealed*");
    }

    [Fact]
    public void Evidence_cannot_be_added_after_a_decision()
    {
        var request = Queued();
        request.Approve(Reviewer, "Compliance", null, Now);

        var attach = () => request.AttachDocument(
            VerificationDocumentType.UtilityBill, "storage/late", Now);

        attach.Should().Throw<DomainException>()
            .WithMessage("*cannot be attached*");
    }

    [Fact]
    public void Re_attaching_a_document_type_supersedes_rather_than_replaces()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Country, Now);

        var first = request.AttachDocument(VerificationDocumentType.Passport, "storage/v1", Now);
        var second = request.AttachDocument(VerificationDocumentType.Passport, "storage/v2", Now);

        first.Status.Should().Be(VerificationDocumentStatus.Superseded,
            "a reviewer must still be able to see what was originally submitted");
        second.Status.Should().Be(VerificationDocumentStatus.Pending);
        second.Version.Should().Be(2);
        request.Documents.Should().HaveCount(2);
    }

    [Fact]
    public void Approval_accepts_every_pending_document()
    {
        var request = Queued();

        request.Approve(Reviewer, "Compliance", "Looks good.", Now);

        request.Documents.Should().OnlyContain(
            document => document.Status == VerificationDocumentStatus.Accepted);
    }

    [Fact]
    public void Every_transition_is_recorded_as_a_review()
    {
        var request = Queued();

        request.Assign(Reviewer, Now);
        request.RequestMoreInformation(Reviewer, "Compliance", "Please re-upload.", Now);

        request.Reviews.Should().ContainSingle()
            .Which.ToStatus.Should().Be(VerificationStatus.NeedMoreInfo);
    }

    [Fact]
    public void Releasing_an_assigned_request_returns_it_to_the_queue()
    {
        var request = Queued();
        request.Assign(Reviewer, Now);
        request.Status.Should().Be(VerificationStatus.UnderReview);

        request.Release(Now);

        request.Status.Should().Be(VerificationStatus.Pending);
        request.AssignedReviewerId.Should().BeNull();
    }

    [Fact]
    public void An_overdue_request_is_reported_as_overdue()
    {
        var request = Queued();

        request.IsOverdue(Now.AddHours(47)).Should().BeFalse();
        request.IsOverdue(Now.AddHours(49)).Should().BeTrue();
    }

    [Fact]
    public void A_decided_request_is_never_overdue()
    {
        var request = Queued();
        request.Approve(Reviewer, "Compliance", null, Now);

        request.IsOverdue(Now.AddYears(1)).Should().BeFalse(
            "an overdue queue should not fill up with items nobody needs to act on");
    }

    [Theory]
    [InlineData(VerificationStatus.Approved, VerificationStatus.Expired, true)]
    [InlineData(VerificationStatus.Approved, VerificationStatus.Revoked, true)]
    [InlineData(VerificationStatus.Approved, VerificationStatus.Rejected, false)]
    [InlineData(VerificationStatus.Pending, VerificationStatus.Approved, true)]
    [InlineData(VerificationStatus.Expired, VerificationStatus.Approved, false)]
    [InlineData(VerificationStatus.Revoked, VerificationStatus.Approved, false)]
    public void The_transition_table_is_the_single_source_of_truth(
        VerificationStatus from, VerificationStatus to, bool allowed) =>
        VerificationWorkflow.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void Grant_statuses_are_exactly_approved_and_partially_approved()
    {
        VerificationWorkflow.IsGrant(VerificationStatus.Approved).Should().BeTrue();
        VerificationWorkflow.IsGrant(VerificationStatus.PartiallyApproved).Should().BeTrue();
        VerificationWorkflow.IsGrant(VerificationStatus.Pending).Should().BeFalse();
        VerificationWorkflow.IsGrant(VerificationStatus.Expired).Should().BeFalse();
    }
}

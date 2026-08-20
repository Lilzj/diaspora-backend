using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.UnitTests.Jobs.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Jobs;

/// <summary>
/// The hiring workflow, and the asymmetry that is its whole design: an employer walks an
/// application forward one legal step at a time; a candidate may leave from anywhere.
/// </summary>
public class JobApplicationWorkflowTests
{
    private static readonly DateTimeOffset Now = JobFixtures.Now;

    [Fact]
    public void A_submitted_application_starts_active_and_unread()
    {
        var application = JobFixtures.Application();

        application.Status.Should().Be(ApplicationStatus.Submitted);
        application.IsActive.Should().BeTrue();
        application.CanWithdraw.Should().BeTrue();
        application.HasBeenViewed.Should().BeFalse();
        application.TimeToFirstResponse.Should().BeNull(
            "nobody has responded, which is not the same as responding instantly");
    }

    [Fact]
    public void Submission_is_recorded_on_the_history_immediately()
    {
        var application = JobFixtures.Application();

        application.History.Should().ContainSingle()
            .Which.Kind.Should().Be(ApplicationEventKind.Submitted);

        application.CandidateVisibleHistory.Should().ContainSingle(
            "the candidate can see that they applied");
    }

    // ── The employer's path ─────────────────────────────────────────────────────────────────

    [Fact]
    public void An_employer_walks_the_application_forward_one_step_at_a_time()
    {
        var application = JobFixtures.Application();
        var recruiter = JobFixtures.NewUserId();

        application.MoveTo(ApplicationStatus.UnderReview, Now, recruiter)
            .IsSuccess.Should().BeTrue();
        application.MoveTo(ApplicationStatus.Shortlisted, Now, recruiter)
            .IsSuccess.Should().BeTrue();
        application.MoveTo(ApplicationStatus.InterviewScheduled, Now, recruiter)
            .IsSuccess.Should().BeTrue();

        application.Status.Should().Be(ApplicationStatus.InterviewScheduled);
    }

    [Fact]
    public void Skipping_straight_to_accepted_is_refused()
    {
        var application = JobFixtures.Application();

        application.MoveTo(ApplicationStatus.Accepted, Now, JobFixtures.NewUserId())
            .IsFailure.Should().BeTrue("acceptance only follows an offer");
    }

    /// <summary>
    /// The employer's mutator cannot reach Withdrawn. That is the candidate's decision and must
    /// not be issuable by passing an enum value.
    /// </summary>
    [Fact]
    public void An_employer_cannot_withdraw_on_the_candidates_behalf()
    {
        var application = JobFixtures.Application();

        application.MoveTo(ApplicationStatus.Withdrawn, Now, JobFixtures.NewUserId())
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_decided_application_cannot_move_again()
    {
        var application = JobFixtures.Application();
        application.MoveTo(ApplicationStatus.Rejected, Now, JobFixtures.NewUserId());

        application.MoveTo(ApplicationStatus.Shortlisted, Now, JobFixtures.NewUserId())
            .IsFailure.Should().BeTrue();
    }

    // ── The candidate's exit ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.InterviewCompleted)]
    [InlineData(ApplicationStatus.OfferExtended)]
    public void A_candidate_can_withdraw_from_anywhere_before_an_ending(
        ApplicationStatus reached)
    {
        var application = JobFixtures.Application();
        var recruiter = JobFixtures.NewUserId();

        WalkTo(application, reached, recruiter);

        application.Withdraw(Now, application.ApplicantUserId).IsSuccess.Should().BeTrue(
            "anything else would make the platform a trap for the party with the least power");

        application.Status.Should().Be(ApplicationStatus.Withdrawn);
        application.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Withdrawing_twice_is_refused()
    {
        var application = JobFixtures.Application();
        application.Withdraw(Now, application.ApplicantUserId);

        application.Withdraw(Now, application.ApplicantUserId).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_decided_application_cannot_be_withdrawn()
    {
        var application = JobFixtures.Application();
        application.MoveTo(ApplicationStatus.Rejected, Now, JobFixtures.NewUserId());

        application.Withdraw(Now, application.ApplicantUserId).IsFailure.Should().BeTrue();
    }

    /// <summary>Withdrawing needs no justification. Requiring one would be a small cruelty.</summary>
    [Fact]
    public void Withdrawing_without_a_reason_succeeds()
    {
        var application = JobFixtures.Application();

        application.Withdraw(Now, application.ApplicantUserId, reason: null)
            .IsSuccess.Should().BeTrue();
    }

    // ── The history ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An employer's internal note belongs on the record and emphatically not in front of the
    /// candidate. Without the flag the platform would have to choose between hiding the timeline
    /// entirely and publishing every private remark.
    /// </summary>
    [Fact]
    public void An_internal_note_never_reaches_the_candidates_timeline()
    {
        var application = JobFixtures.Application();

        application.AddNote(
            Guid.NewGuid(),
            JobFixtures.NewUserId(),
            "Strong, but we already have someone",
            Now).IsSuccess.Should().BeTrue();

        application.History.Should().HaveCount(2);
        application.CandidateVisibleHistory.Should().HaveCount(
            1, "the note is the employer's alone");
    }

    /// <summary>
    /// A rejection reason the employer marked private stays on the record and off the candidate's
    /// view — which is the honest option when the real reason is not one to send.
    /// </summary>
    [Fact]
    public void A_private_decision_reason_is_not_shown_to_the_candidate()
    {
        var application = JobFixtures.Application();

        application.MoveTo(
            ApplicationStatus.Rejected,
            Now,
            JobFixtures.NewUserId(),
            "Overqualified and we cannot match the salary",
            reasonVisibleToCandidate: false);

        application.DecisionReason.Should().BeNull();
    }

    [Fact]
    public void A_shared_decision_reason_is_shown_to_the_candidate()
    {
        var application = JobFixtures.Application();

        application.MoveTo(
            ApplicationStatus.Rejected,
            Now,
            JobFixtures.NewUserId(),
            "We have filled the role",
            reasonVisibleToCandidate: true);

        application.DecisionReason.Should().Be("We have filled the role");
    }

    // ── Response measurement ────────────────────────────────────────────────────────────────

    /// <summary>
    /// True exactly once per application, which is what lets a running average be maintained on
    /// the employer without storing every interval.
    /// </summary>
    [Fact]
    public void Only_the_first_employer_move_counts_as_a_response()
    {
        var application = JobFixtures.Application();
        var recruiter = JobFixtures.NewUserId();

        application.MoveTo(ApplicationStatus.UnderReview, Now.AddHours(6), recruiter);
        var afterFirst = application.TimeToFirstResponse;

        application.MoveTo(ApplicationStatus.Shortlisted, Now.AddDays(3), recruiter);

        application.TimeToFirstResponse.Should().Be(
            afterFirst, "the measurement is of the first response, not the latest");

        afterFirst!.Value.TotalHours.Should().BeApproximately(6d, 0.01);
    }

    /// <summary>Idempotent: a recruiter refreshing a list must not produce a thousand events.</summary>
    [Fact]
    public void Only_the_first_view_is_recorded()
    {
        var application = JobFixtures.Application();
        var recruiter = JobFixtures.NewUserId();

        application.RecordViewed(Now.AddHours(2), recruiter);
        application.RecordViewed(Now.AddHours(3), recruiter);
        application.RecordViewed(Now.AddHours(4), recruiter);

        application.FirstViewedAt.Should().Be(Now.AddHours(2));
        application.History.Count(entry => entry.Kind == ApplicationEventKind.Viewed)
            .Should().Be(1);
    }

    // ── The transition table ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.OfferExtended, false)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.OfferExtended, true)]
    [InlineData(ApplicationStatus.InterviewScheduled, ApplicationStatus.Shortlisted, true)]
    [InlineData(ApplicationStatus.InterviewCompleted, ApplicationStatus.UnderReview, false)]
    [InlineData(ApplicationStatus.OfferExtended, ApplicationStatus.Accepted, true)]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Rejected, false)]
    public void The_transition_table_matches_the_documented_machine(
        ApplicationStatus from, ApplicationStatus to, bool allowed) =>
        ApplicationStatusTransitions.CanTransition(from, to).Should().Be(allowed);

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.InterviewScheduled)]
    [InlineData(ApplicationStatus.InterviewCompleted)]
    [InlineData(ApplicationStatus.OfferExtended)]
    public void Withdrawal_is_reachable_from_every_non_terminal_state(ApplicationStatus from) =>
        ApplicationStatusTransitions
            .CanTransition(from, ApplicationStatus.Withdrawn).Should().BeTrue();

    [Theory]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void Nothing_moves_out_of_a_terminal_state(ApplicationStatus terminal)
    {
        ApplicationStatusTransitions.IsTerminal(terminal).Should().BeTrue();
        ApplicationStatusTransitions.From(terminal).Should().BeEmpty();
    }

    /// <summary>Walks an application to a status through the legal path.</summary>
    private static void WalkTo(
        JobApplication application,
        ApplicationStatus target,
        Diaspora.SharedKernel.Identity.UserId recruiter)
    {
        var path = target switch
        {
            ApplicationStatus.Submitted => Array.Empty<ApplicationStatus>(),
            ApplicationStatus.UnderReview => [ApplicationStatus.UnderReview],
            ApplicationStatus.Shortlisted =>
                [ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted],
            ApplicationStatus.InterviewCompleted =>
            [
                ApplicationStatus.UnderReview,
                ApplicationStatus.Shortlisted,
                ApplicationStatus.InterviewScheduled,
                ApplicationStatus.InterviewCompleted
            ],
            ApplicationStatus.OfferExtended =>
            [
                ApplicationStatus.UnderReview,
                ApplicationStatus.Shortlisted,
                ApplicationStatus.OfferExtended
            ],
            _ => []
        };

        foreach (var step in path)
        {
            application.MoveTo(step, JobFixtures.Now, recruiter).IsSuccess.Should().BeTrue(
                $"the walk to {target} must itself be legal");
        }
    }
}

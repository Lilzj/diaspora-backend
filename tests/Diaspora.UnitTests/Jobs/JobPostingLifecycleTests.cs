using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.UnitTests.Jobs.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Jobs;

/// <summary>
/// The posting lifecycle, which is where most of the module's rules live.
/// </summary>
public class JobPostingLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_posting_is_a_draft_nobody_outside_the_employer_can_see()
    {
        var posting = JobFixtures.Posting();

        posting.Status.Should().Be(JobStatus.Draft);
        posting.IsDiscoverable.Should().BeFalse();
        posting.AcceptsApplications(Now).Should().BeFalse();
    }

    [Fact]
    public void Publishing_opens_it_for_applications_by_default()
    {
        var posting = JobFixtures.Posting();

        posting.Publish(Now).IsSuccess.Should().BeTrue();

        posting.Status.Should().Be(JobStatus.Open);
        posting.IsDiscoverable.Should().BeTrue();
        posting.AcceptsApplications(Now).Should().BeTrue();
        posting.PublishedAt.Should().Be(Now);
        posting.FirstPublishedAt.Should().Be(Now);
    }

    /// <summary>
    /// Published and Open are genuinely different states. An employer advertising a role before
    /// the application window opens is doing something ordinary.
    /// </summary>
    [Fact]
    public void Publishing_without_opening_makes_it_visible_but_shut()
    {
        var posting = JobFixtures.Posting();

        posting.Publish(Now, publishedBy: null, openImmediately: false)
            .IsSuccess.Should().BeTrue();

        posting.Status.Should().Be(JobStatus.Published);
        posting.IsDiscoverable.Should().BeTrue("it is advertised");
        posting.AcceptsApplications(Now).Should().BeFalse("the window has not opened");
    }

    /// <summary>
    /// <b>Every posting the factory produces is already publishable</b>, because the four things
    /// publication requires — a title, a body, a category and a country — are all constructor
    /// arguments that cannot be empty. <c>IsPublishable</c> is therefore defence in depth against
    /// a future mutator rather than a check the current API can fail, and stating that is more
    /// useful than a test contriving an unreachable state.
    /// </summary>
    [Fact]
    public void A_posting_is_publishable_from_the_moment_it_exists()
    {
        var posting = JobFixtures.Posting();

        posting.IsPublishable.Should().BeTrue(
            "the aggregate refuses to construct a posting missing anything publication needs");
    }

    /// <summary>The guard itself, exercised directly since the factory cannot violate it.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Ward nursing on a busy acute unit.", true)]
    public void A_description_must_have_content(string body, bool valid) =>
        JobDescription.Create(body).IsSuccess.Should().Be(valid);

    [Fact]
    public void Publishing_with_a_closing_date_in_the_past_is_refused()
    {
        var posting = JobFixtures.Posting();

        posting.SetClosingDate(Now.AddDays(5), Now, baseRevision: null)
            .IsSuccess.Should().BeTrue();

        // The clock moved past the date before anybody published.
        posting.Publish(Now.AddDays(10)).IsFailure.Should().BeTrue(
            "publishing an already-shut vacancy would fire every matching alert for nothing");
    }

    // ── Vacancies ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A posting advertising six seats fills five without its status changing. Conflating the
    /// vacancy event and the status transition is easy on the common single-seat posting.
    /// </summary>
    [Fact]
    public void Filling_one_of_several_seats_does_not_close_the_posting()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        SetVacancies(posting, 3);

        posting.FillVacancy(JobFixtures.NewApplicationId(), Now).IsSuccess.Should().BeTrue();

        posting.FilledCount.Should().Be(1);
        posting.RemainingVacancies.Should().Be(2);
        posting.Status.Should().Be(JobStatus.Open);
        posting.AcceptsApplications(Now).Should().BeTrue();
    }

    [Fact]
    public void Filling_the_last_seat_closes_the_posting()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);

        posting.FillVacancy(JobFixtures.NewApplicationId(), Now).IsSuccess.Should().BeTrue();

        posting.Status.Should().Be(JobStatus.Filled);
        posting.RemainingVacancies.Should().Be(0);
        posting.AcceptsApplications(Now).Should().BeFalse();
    }

    [Fact]
    public void A_filled_posting_never_reopens()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.FillVacancy(JobFixtures.NewApplicationId(), Now);

        posting.MoveTo(JobStatus.Open, Now, changedBy: null, reason: null)
            .IsFailure.Should().BeTrue(
                "reopening would rewrite the history of everyone who applied to the first");
    }

    // ── Expiry ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Expiry is computed from the clock, not from a flag. A posting past its closing date is
    /// shut whether or not the sweep has run.
    /// </summary>
    [Fact]
    public void A_posting_past_its_closing_date_stops_accepting_before_any_sweep_runs()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.SetClosingDate(Now.AddDays(1), Now, baseRevision: null);

        posting.Status.Should().Be(JobStatus.Open, "nothing has written the expiry down");
        posting.AcceptsApplications(Now.AddDays(2)).Should().BeFalse(
            "the date has passed, and that is true regardless of what the status column says");
    }

    /// <summary>
    /// Reversible on purpose: the alternative is the employer archiving and recreating, which
    /// loses every application attached.
    /// </summary>
    [Fact]
    public void Extending_the_closing_date_reopens_an_expired_posting()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.SetClosingDate(Now.AddDays(1), Now, baseRevision: null);
        posting.Expire(Now.AddDays(2));

        posting.Status.Should().Be(JobStatus.Expired);

        posting.SetClosingDate(Now.AddDays(30), Now.AddDays(2), baseRevision: null)
            .IsSuccess.Should().BeTrue();

        posting.Status.Should().Be(JobStatus.Open);
    }

    [Fact]
    public void Expiring_a_posting_whose_date_has_not_passed_does_nothing()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.SetClosingDate(Now.AddDays(10), Now, baseRevision: null);

        posting.Expire(Now.AddDays(1)).IsSuccess.Should().BeTrue();

        posting.Status.Should().Be(JobStatus.Open, "the sweep runs speculatively over batches");
    }

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_stale_editor_is_refused_with_a_conflict()
    {
        var posting = JobFixtures.Posting();
        var original = posting.Revision;

        posting.SetTags(["nursing"], baseRevision: original).IsSuccess.Should().BeTrue();

        posting.SetTags(["care"], baseRevision: original)
            .IsFailure.Should().BeTrue("the revision moved under them");
    }

    /// <summary>
    /// A candidate viewing a job must never make a recruiter's in-flight save fail.
    /// </summary>
    [Fact]
    public void Counters_do_not_bump_the_revision()
    {
        var posting = JobFixtures.Posting();
        var revision = posting.Revision;

        posting.RecordView(unique: true);
        posting.ApplySaveDelta(1);
        posting.ApplyApplicationDelta(1);
        posting.RecordShare();

        posting.Revision.Should().Be(revision);
        posting.Engagement.ViewCount.Should().Be(1);
        posting.Engagement.SaveCount.Should().Be(1);
    }

    /// <summary>
    /// A projection arriving from Trust must not produce a conflict a human cannot explain.
    /// </summary>
    [Fact]
    public void A_trust_decision_does_not_bump_the_revision()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        var revision = posting.Revision;

        posting.ApplyVerificationDecision(
            JobVerificationState.Verified, Guid.NewGuid(), "Verified employer", null, Now);

        posting.TrustMark.IsVerified.Should().BeTrue();
        posting.Revision.Should().Be(revision);
    }

    // ── Trust projection ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_but_a_trust_decision_can_verify_a_posting()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);

        posting.SubmitForVerification(Now, JobFixtures.NewUserId()).IsSuccess.Should().BeTrue();

        posting.TrustMark.State.Should().Be(JobVerificationState.Pending);
        posting.TrustMark.IsVerified.Should().BeFalse("submitting is not approval");
    }

    /// <summary>
    /// Delivery is at-least-once and unordered. A redelivered old approval must not re-verify a
    /// posting Trust has since revoked.
    /// </summary>
    [Fact]
    public void A_stale_trust_decision_is_dropped()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);

        posting.ApplyVerificationDecision(
            JobVerificationState.Revoked, null, null, "Fraudulent", Now.AddHours(2));

        posting.ApplyVerificationDecision(
            JobVerificationState.Verified, null, "Verified", null, Now.AddHours(1));

        posting.TrustMark.State.Should().Be(
            JobVerificationState.Revoked, "the older approval must not win");
    }

    [Fact]
    public void Submitting_twice_while_pending_is_refused()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.SubmitForVerification(Now, JobFixtures.NewUserId());

        posting.SubmitForVerification(Now, JobFixtures.NewUserId()).IsFailure.Should().BeTrue();
    }

    // ── Moderation ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Hiding_takes_a_posting_off_public_surfaces_without_changing_its_status()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);

        posting.Hide("Fraudulent advert", JobFixtures.NewUserId(), Now).IsSuccess.Should().BeTrue();

        posting.ModerationState.Should().Be(JobModerationState.Hidden);
        posting.IsDiscoverable.Should().BeFalse();
        posting.Status.Should().Be(JobStatus.Open, "lifting the hide must restore what was there");
        posting.AcceptsApplications(Now).Should().BeFalse();
    }

    [Fact]
    public void Hiding_without_a_reason_is_refused()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);

        posting.Hide(reason: null, JobFixtures.NewUserId(), Now).IsFailure.Should().BeTrue(
            "a takedown nobody can account for six months later is one nobody can review");
    }

    [Fact]
    public void Unhiding_restores_discoverability()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.Hide("Under review", JobFixtures.NewUserId(), Now);

        posting.Unhide(JobFixtures.NewUserId(), Now).IsSuccess.Should().BeTrue();

        posting.ModerationState.Should().Be(JobModerationState.Clear);
        posting.IsDiscoverable.Should().BeTrue();
    }

    // ── Archival ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_archived_posting_cannot_be_edited()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.MoveTo(JobStatus.Archived, Now, changedBy: null, reason: null);

        posting.SetTags(["nursing"], baseRevision: null).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Archiving_drops_featured_placement()
    {
        var posting = JobFixtures.Posting();
        posting.Publish(Now);
        posting.Feature(Now, JobFixtures.NewUserId());

        posting.MoveTo(JobStatus.Archived, Now, changedBy: null, reason: null);

        posting.IsFeatured.Should().BeFalse(
            "otherwise restoring it would resurface it in a featured slot");
    }

    [Fact]
    public void A_draft_cannot_be_featured()
    {
        var posting = JobFixtures.Posting();

        posting.Feature(Now, JobFixtures.NewUserId()).IsFailure.Should().BeTrue();
    }

    // ── The transition table ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(JobStatus.Draft, JobStatus.Published, true)]
    [InlineData(JobStatus.Draft, JobStatus.Open, true)]
    [InlineData(JobStatus.Draft, JobStatus.Filled, false)]
    [InlineData(JobStatus.Open, JobStatus.ReceivingApplications, true)]
    [InlineData(JobStatus.OfferMade, JobStatus.Interviewing, true)]
    [InlineData(JobStatus.Filled, JobStatus.Open, false)]
    [InlineData(JobStatus.Filled, JobStatus.Archived, true)]
    [InlineData(JobStatus.Expired, JobStatus.Open, true)]
    [InlineData(JobStatus.Archived, JobStatus.Draft, true)]
    public void The_transition_table_matches_the_documented_machine(
        JobStatus from, JobStatus to, bool allowed) =>
        JobStatusTransitions.CanTransition(from, to).Should().Be(allowed);

    [Theory]
    [InlineData(JobStatus.Open, true)]
    [InlineData(JobStatus.ReceivingApplications, true)]
    [InlineData(JobStatus.Interviewing, true)]
    [InlineData(JobStatus.Published, false)]
    [InlineData(JobStatus.Filled, false)]
    [InlineData(JobStatus.Expired, false)]
    public void Only_the_open_states_accept_applications(JobStatus status, bool accepts) =>
        JobStatusTransitions.AcceptsApplications(status).Should().Be(accepts);

    /// <summary>
    /// Vacancies are set through the update path, which is where the invariant lives.
    /// </summary>
    private static void SetVacancies(JobPosting posting, int count) =>
        posting.UpdateDetails(
            posting.Title,
            posting.Description,
            posting.JobType,
            posting.ExperienceLevel,
            posting.EducationLevel,
            posting.Location,
            posting.WorkArrangement,
            posting.Salary,
            posting.Visibility,
            count,
            posting.RequiresResume,
            baseRevision: null);
}
